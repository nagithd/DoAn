#!/usr/bin/env python3
"""TensorRT battery detector used by the DOFBOT monitor-only camera.

This process runs on the Jetson host (JetPack 4.6 / TensorRT 8), not in the
Yahboom Docker container.  The container keeps ownership of /dev/video0 and
posts JPEG frames to this local service.  The service performs inference only;
it has no robot, conveyor, or motion endpoints.
"""

import argparse
import ctypes
import ctypes.util
import json
import logging
import signal
import socketserver
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, HTTPServer

import cv2
import numpy as np
import tensorrt as trt


LOGGER = logging.getLogger("dofbot-trt-inference")
MAX_REQUEST_BYTES = 8 * 1024 * 1024


class CudaRuntime(object):
    """Minimal CUDA Runtime wrapper for JetPack 4.6.

    Using the CUDA Runtime directly avoids adding a separately built PyCUDA
    wheel to the vendor Python/TensorRT installation.
    """

    COPY_HOST_TO_DEVICE = 1
    COPY_DEVICE_TO_HOST = 2

    def __init__(self):
        candidates = [
            ctypes.util.find_library("cudart"),
            "libcudart.so",
            "libcudart.so.10.2",
            "/usr/local/cuda/lib64/libcudart.so.10.2",
        ]
        self._library = None
        errors = []
        for candidate in candidates:
            if not candidate:
                continue
            try:
                self._library = ctypes.CDLL(candidate)
                break
            except OSError as exc:
                errors.append("{}: {}".format(candidate, exc))
        if self._library is None:
            raise RuntimeError(
                "Cannot load the JetPack CUDA runtime: " + "; ".join(errors)
            )

        self._library.cudaSetDevice.argtypes = [ctypes.c_int]
        self._library.cudaSetDevice.restype = ctypes.c_int
        self._library.cudaMalloc.argtypes = [
            ctypes.POINTER(ctypes.c_void_p),
            ctypes.c_size_t,
        ]
        self._library.cudaMalloc.restype = ctypes.c_int
        self._library.cudaFree.argtypes = [ctypes.c_void_p]
        self._library.cudaFree.restype = ctypes.c_int
        self._library.cudaMemcpy.argtypes = [
            ctypes.c_void_p,
            ctypes.c_void_p,
            ctypes.c_size_t,
            ctypes.c_int,
        ]
        self._library.cudaMemcpy.restype = ctypes.c_int
        self._library.cudaDeviceSynchronize.argtypes = []
        self._library.cudaDeviceSynchronize.restype = ctypes.c_int
        self._library.cudaGetErrorString.argtypes = [ctypes.c_int]
        self._library.cudaGetErrorString.restype = ctypes.c_char_p
        self._check(self._library.cudaSetDevice(0), "cudaSetDevice")

    def _check(self, code, operation):
        if code == 0:
            return
        message = self._library.cudaGetErrorString(code)
        detail = message.decode("utf-8") if message else "unknown CUDA error"
        raise RuntimeError("{} failed: {} ({})".format(
            operation,
            detail,
            code,
        ))

    def malloc(self, size):
        pointer = ctypes.c_void_p()
        self._check(
            self._library.cudaMalloc(ctypes.byref(pointer), int(size)),
            "cudaMalloc",
        )
        return pointer

    def free(self, pointer):
        if pointer and pointer.value:
            self._check(self._library.cudaFree(pointer), "cudaFree")

    def copy_host_to_device(self, device, host):
        self._check(
            self._library.cudaMemcpy(
                device,
                ctypes.c_void_p(int(host.ctypes.data)),
                int(host.nbytes),
                self.COPY_HOST_TO_DEVICE,
            ),
            "cudaMemcpy host-to-device",
        )

    def copy_device_to_host(self, host, device):
        self._check(
            self._library.cudaMemcpy(
                ctypes.c_void_p(int(host.ctypes.data)),
                device,
                int(host.nbytes),
                self.COPY_DEVICE_TO_HOST,
            ),
            "cudaMemcpy device-to-host",
        )

    def synchronize(self):
        self._check(
            self._library.cudaDeviceSynchronize(),
            "cudaDeviceSynchronize",
        )


class TensorRTBatteryDetector(object):
    def __init__(self, engine_path, confidence=0.45, iou=0.50, max_det=10):
        self.engine_path = engine_path
        self.confidence = float(confidence)
        self.iou = float(iou)
        self.max_det = int(max_det)
        self._lock = threading.Lock()

        self._cuda = CudaRuntime()
        trt_logger = trt.Logger(trt.Logger.WARNING)
        with open(engine_path, "rb") as engine_file:
            runtime = trt.Runtime(trt_logger)
            self._engine = runtime.deserialize_cuda_engine(
                engine_file.read()
            )
        if self._engine is None:
            raise RuntimeError("TensorRT could not deserialize the engine")

        self._context = self._engine.create_execution_context()
        self._bindings = [0] * self._engine.num_bindings
        self._buffers = {}
        self._input_index = None
        self._output_indices = []

        for index in range(self._engine.num_bindings):
            name = self._engine.get_binding_name(index)
            shape = tuple(
                int(value)
                for value in self._engine.get_binding_shape(index)
            )
            if any(value <= 0 for value in shape):
                raise RuntimeError(
                    "Only a static TensorRT engine is supported; "
                    "binding {} has shape {}".format(name, shape)
                )
            dtype = trt.nptype(self._engine.get_binding_dtype(index))
            size = int(trt.volume(shape))
            host = np.empty(size, dtype=dtype)
            device = self._cuda.malloc(host.nbytes)
            self._bindings[index] = int(device.value)
            self._buffers[index] = {
                "name": name,
                "shape": shape,
                "dtype": dtype,
                "host": host,
                "device": device,
            }
            if self._engine.binding_is_input(index):
                if self._input_index is not None:
                    raise RuntimeError("Expected exactly one engine input")
                self._input_index = index
            else:
                self._output_indices.append(index)

        if self._input_index is None or not self._output_indices:
            raise RuntimeError("Invalid TensorRT input/output bindings")

        input_shape = self._buffers[self._input_index]["shape"]
        if len(input_shape) != 4 or input_shape[0] != 1:
            raise RuntimeError(
                "Expected a static NCHW batch-1 input, got {}".format(
                    input_shape
                )
            )
        self.input_height = int(input_shape[2])
        self.input_width = int(input_shape[3])
        LOGGER.info(
            "Loaded %s; input=%s; outputs=%s",
            engine_path,
            input_shape,
            [
                self._buffers[index]["shape"]
                for index in self._output_indices
            ],
        )

    @staticmethod
    def _letterbox(image, target_width, target_height):
        source_height, source_width = image.shape[:2]
        scale = min(
            float(target_width) / float(source_width),
            float(target_height) / float(source_height),
        )
        resized_width = max(1, int(round(source_width * scale)))
        resized_height = max(1, int(round(source_height * scale)))
        resized = cv2.resize(
            image,
            (resized_width, resized_height),
            interpolation=cv2.INTER_LINEAR,
        )
        pad_x = (target_width - resized_width) // 2
        pad_y = (target_height - resized_height) // 2
        canvas = np.full(
            (target_height, target_width, 3),
            114,
            dtype=np.uint8,
        )
        canvas[
            pad_y:pad_y + resized_height,
            pad_x:pad_x + resized_width,
        ] = resized
        return canvas, scale, pad_x, pad_y

    def _preprocess(self, image):
        boxed, scale, pad_x, pad_y = self._letterbox(
            image,
            self.input_width,
            self.input_height,
        )
        rgb = cv2.cvtColor(boxed, cv2.COLOR_BGR2RGB)
        tensor = rgb.astype(np.float32) / 255.0
        tensor = np.transpose(tensor, (2, 0, 1))[None, ...]
        return np.ascontiguousarray(tensor), scale, pad_x, pad_y

    @staticmethod
    def _candidate_matrix(output):
        array = np.asarray(output)
        while array.ndim > 2 and array.shape[0] == 1:
            array = array[0]
        if array.ndim != 2:
            return None
        # Ultralytics detection export is normally [5, anchors] for one class.
        if array.shape[0] <= 16 and array.shape[1] > array.shape[0]:
            array = array.T
        if array.shape[1] < 5:
            return None
        return array

    def _decode(self, outputs, image_shape, scale, pad_x, pad_y):
        matrices = []
        for output in outputs:
            matrix = self._candidate_matrix(output)
            if matrix is not None:
                matrices.append(matrix)
        if not matrices:
            raise RuntimeError(
                "Unsupported YOLO output shapes: {}".format(
                    [tuple(output.shape) for output in outputs]
                )
            )

        predictions = np.concatenate(matrices, axis=0)
        scores = predictions[:, 4]
        accepted = np.where(scores >= self.confidence)[0]
        if accepted.size == 0:
            return []

        source_height, source_width = image_shape[:2]
        boxes_xywh = []
        confidences = []
        for index in accepted:
            center_x, center_y, width, height = (
                float(value) for value in predictions[index, :4]
            )
            left = (center_x - width / 2.0 - pad_x) / scale
            top = (center_y - height / 2.0 - pad_y) / scale
            right = (center_x + width / 2.0 - pad_x) / scale
            bottom = (center_y + height / 2.0 - pad_y) / scale
            left = max(0.0, min(float(source_width - 1), left))
            top = max(0.0, min(float(source_height - 1), top))
            right = max(0.0, min(float(source_width - 1), right))
            bottom = max(0.0, min(float(source_height - 1), bottom))
            box_width = max(0.0, right - left)
            box_height = max(0.0, bottom - top)
            if box_width < 2.0 or box_height < 2.0:
                continue
            boxes_xywh.append([
                int(round(left)),
                int(round(top)),
                int(round(box_width)),
                int(round(box_height)),
            ])
            confidences.append(float(scores[index]))

        if not boxes_xywh:
            return []
        kept = cv2.dnn.NMSBoxes(
            boxes_xywh,
            confidences,
            self.confidence,
            self.iou,
        )
        if kept is None or len(kept) == 0:
            return []
        kept_indices = np.asarray(kept).reshape(-1).tolist()
        detections = []
        for index in kept_indices[:self.max_det]:
            left, top, width, height = boxes_xywh[int(index)]
            detections.append({
                "class_id": 0,
                "class_name": "battery",
                "confidence": round(confidences[int(index)], 6),
                "bbox": {
                    "x": left,
                    "y": top,
                    "width": width,
                    "height": height,
                },
                "center_x": round(left + width / 2.0, 1),
                "center_y": round(top + height / 2.0, 1),
            })
        detections.sort(key=lambda item: item["confidence"], reverse=True)
        return detections

    def infer(self, image):
        # Use a monotonic clock. Jetson systems without a battery-backed RTC
        # can jump from their fallback 2023 timestamp to NTP time while the
        # service is starting, which makes wall-clock inference durations
        # appear to span years.
        started = time.perf_counter()
        tensor, scale, pad_x, pad_y = self._preprocess(image)
        with self._lock:
            input_buffer = self._buffers[self._input_index]
            np.copyto(input_buffer["host"], tensor.ravel())
            self._cuda.copy_host_to_device(
                input_buffer["device"],
                input_buffer["host"],
            )
            if not self._context.execute_v2(bindings=self._bindings):
                raise RuntimeError("TensorRT execute_v2 failed")
            self._cuda.synchronize()
            for index in self._output_indices:
                output_buffer = self._buffers[index]
                self._cuda.copy_device_to_host(
                    output_buffer["host"],
                    output_buffer["device"],
                )
            outputs = [
                self._buffers[index]["host"].reshape(
                    self._buffers[index]["shape"]
                ).copy()
                for index in self._output_indices
            ]

        detections = self._decode(
            outputs,
            image.shape,
            scale,
            pad_x,
            pad_y,
        )
        return {
            "success": True,
            "detector": "yolov8n-tensorrt-fp16",
            "monitor_only": True,
            "inference_ms": round(
                (time.perf_counter() - started) * 1000.0,
                2,
            ),
            "detections": detections,
            "output_shapes": [list(output.shape) for output in outputs],
        }


class ThreadedHTTPServer(socketserver.ThreadingMixIn, HTTPServer):
    daemon_threads = True


class InferenceHandler(BaseHTTPRequestHandler):
    detector = None

    def _write_json(self, status, value):
        payload = json.dumps(value).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_GET(self):
        if self.path.rstrip("/") != "/health":
            self._write_json(404, {"success": False, "error": "Not found"})
            return
        detector = self.detector
        self._write_json(200, {
            "success": True,
            "status": "ok",
            "service": "dofbot-trt-inference",
            "monitor_only": True,
            "routes_robot_motion": False,
            "inference_endpoint": "/infer",
            "engine": detector.engine_path,
            "input_shape": [1, 3, detector.input_height, detector.input_width],
            "confidence": detector.confidence,
            "iou": detector.iou,
        })

    def do_POST(self):
        if self.path.rstrip("/") != "/infer":
            self._write_json(404, {"success": False, "error": "Not found"})
            return
        try:
            content_length = int(self.headers.get("Content-Length", "0"))
            if content_length <= 0 or content_length > MAX_REQUEST_BYTES:
                raise ValueError("Invalid JPEG request size")
            encoded = np.frombuffer(self.rfile.read(content_length), dtype=np.uint8)
            image = cv2.imdecode(encoded, cv2.IMREAD_COLOR)
            if image is None:
                raise ValueError("Request body is not a valid JPEG image")
            self._write_json(200, self.detector.infer(image))
        except ValueError as exc:
            self._write_json(400, {"success": False, "error": str(exc)})
        except Exception as exc:
            LOGGER.exception("TensorRT inference failed")
            self._write_json(500, {"success": False, "error": str(exc)})

    def log_message(self, message_format, *args):
        LOGGER.info("%s - %s", self.address_string(), message_format % args)


def parse_arguments():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--engine",
        default="/home/jetson/models/dofbot_battery_yolov8n_512_fp16.engine",
    )
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=7101)
    parser.add_argument("--confidence", type=float, default=0.45)
    parser.add_argument("--iou", type=float, default=0.50)
    parser.add_argument("--max-det", type=int, default=10)
    parser.add_argument(
        "--warmup-runs",
        type=int,
        default=2,
        help=(
            "Number of black-frame TensorRT warm-up passes to complete "
            "before the HTTP port starts listening"
        ),
    )
    return parser.parse_args()


def main():
    arguments = parse_arguments()
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s | %(levelname)s | %(message)s",
    )
    detector = TensorRTBatteryDetector(
        arguments.engine,
        confidence=arguments.confidence,
        iou=arguments.iou,
        max_det=arguments.max_det,
    )
    if arguments.warmup_runs < 0:
        raise ValueError("warmup-runs must be >= 0")
    if arguments.warmup_runs:
        warmup_frame = np.zeros(
            (detector.input_height, detector.input_width, 3),
            dtype=np.uint8,
        )
        for run_index in range(arguments.warmup_runs):
            warmup_result = detector.infer(warmup_frame)
            LOGGER.info(
                "TensorRT warm-up %d/%d completed in %.2f ms",
                run_index + 1,
                arguments.warmup_runs,
                warmup_result["inference_ms"],
            )
    InferenceHandler.detector = detector
    server = ThreadedHTTPServer(
        (arguments.host, arguments.port),
        InferenceHandler,
    )

    def stop_server(_signum, _frame):
        threading.Thread(target=server.shutdown).start()

    signal.signal(signal.SIGTERM, stop_server)
    signal.signal(signal.SIGINT, stop_server)
    LOGGER.info(
        "DOFBOT TensorRT monitor-only inference listening on http://%s:%d",
        arguments.host,
        arguments.port,
    )
    server.serve_forever()
    server.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
