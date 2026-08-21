from __future__ import annotations

import argparse
import json
import logging
import os
import threading
import time
import uuid
from datetime import datetime, timezone
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any

from ultralytics import YOLO


SERVICE_ROOT = Path(__file__).resolve().parent
DEFAULT_MODEL = SERVICE_ROOT / "models" / "best.pt"
REQUIRED_CLASS_NAMES = frozenset({"dented", "battery", "scratched", "swollen"})
CLASS_NAME_ALIASES = {
    "scratch": "scratched",
    "scratches": "scratched",
}
DEFECT_CLASSES = {"dented", "scratched", "swollen"}


def normalize_model_classes(model_names: Any) -> dict[int, str]:
    """Return model class IDs mapped to the canonical routing names."""
    items = model_names.items() if hasattr(model_names, "items") else enumerate(model_names)
    raw_names = {int(key): str(value) for key, value in items}
    names = {
        class_id: CLASS_NAME_ALIASES.get(name.strip().lower(), name.strip().lower())
        for class_id, name in raw_names.items()
    }
    received_names = set(names.values())
    if len(names) != len(REQUIRED_CLASS_NAMES) or received_names != REQUIRED_CLASS_NAMES:
        raise ValueError(
            "Incompatible model classes. Expected exactly the class names "
            f"{sorted(REQUIRED_CLASS_NAMES)} in any ID order; received {raw_names}."
        )
    return names


class InferenceEngine:
    def __init__(self, model_path: Path, confidence: float, image_size: int, device: str) -> None:
        if not model_path.is_file():
            raise FileNotFoundError(model_path)
        self.model_path = model_path.resolve()
        self.confidence = confidence
        self.image_size = image_size
        self.device = device
        self.model = YOLO(str(self.model_path))
        self.names = normalize_model_classes(self.model.names)
        self.lock = threading.Lock()

    def predict(self, image_path: Path, inspection_id: str | None) -> dict[str, Any]:
        if not image_path.is_file():
            raise FileNotFoundError(image_path)
        if image_path.suffix.lower() not in {".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"}:
            raise ValueError("Unsupported image extension")

        started = time.perf_counter()
        with self.lock:
            predictions = self.model.predict(
                source=str(image_path),
                imgsz=self.image_size,
                conf=self.confidence,
                iou=0.7,
                device=self.device,
                classes=sorted(self.names),
                max_det=300,
                verbose=False,
            )
        elapsed_ms = (time.perf_counter() - started) * 1000.0
        result = predictions[0]

        detections: list[dict[str, Any]] = []
        if result.boxes is not None:
            for box in result.boxes:
                class_id = int(box.cls.item())
                confidence = float(box.conf.item())
                x1, y1, x2, y2 = (float(value) for value in box.xyxy[0].tolist())
                detections.append(
                    {
                        "class_id": class_id,
                        "class_name": self.names[class_id],
                        "confidence": confidence,
                        "x": x1,
                        "y": y1,
                        "width": max(0.0, x2 - x1),
                        "height": max(0.0, y2 - y1),
                    }
                )

        # A battery can contain multiple defects. For the current single-route
        # contract, choose the highest-confidence defect. If no defect is found,
        # a detected battery becomes "normal" (PASS). No battery means no route.
        defects = [item for item in detections if item["class_name"] in DEFECT_CLASSES]
        battery_boxes = [item for item in detections if item["class_name"] == "battery"]
        if defects:
            primary = max(defects, key=lambda item: item["confidence"])
            routing_class = primary["class_name"]
            routing_confidence = primary["confidence"]
            status = "rejected"
            routing_reason = "highest-confidence defect"
        elif battery_boxes:
            primary = max(battery_boxes, key=lambda item: item["confidence"])
            routing_class = "normal"
            routing_confidence = primary["confidence"]
            status = "detected"
            routing_reason = "battery detected without a defect"
        else:
            routing_class = ""
            routing_confidence = 0.0
            status = "no_detection"
            routing_reason = "no battery detected"

        return {
            "inspection_id": inspection_id or str(uuid.uuid4()),
            "status": status,
            "detected_class": routing_class,
            "confidence": routing_confidence,
            "routing_reason": routing_reason,
            "recommended_robot_cycle": {
                "normal": "PASS",
                "dented": "PICK_DENTED",
                "scratched": "PICK_SCRATCHED",
                "swollen": "PICK_SWOLLEN",
            }.get(routing_class, "NONE"),
            "inference_time_ms": elapsed_ms,
            "inspection_time": datetime.now(timezone.utc).isoformat(),
            "image_path": str(image_path.resolve()),
            "image_width": int(result.orig_shape[1]),
            "image_height": int(result.orig_shape[0]),
            "detections": detections,
            "model": {
                "path": str(self.model_path),
                "classes": self.names,
                "confidence_threshold": self.confidence,
                "image_size": self.image_size,
                "device": self.device,
            },
        }

def make_handler(engine: InferenceEngine):
    class Handler(BaseHTTPRequestHandler):
        server_version = "BatteryAI/1.0"
        max_request_size = 4 * 1024 * 1024

        def log_message(self, message: str, *args: object) -> None:
            logging.info("%s | %s", self.client_address[0], message % args)

        def send_json(self, status: HTTPStatus, body: dict[str, Any]) -> None:
            payload = json.dumps(body, ensure_ascii=False).encode("utf-8")
            self.send_response(status.value)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(payload)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(payload)

        def do_GET(self) -> None:
            if self.path.rstrip("/") != "/health":
                self.send_json(HTTPStatus.NOT_FOUND, {"status": "error", "error": "not found"})
                return
            self.send_json(
                HTTPStatus.OK,
                {
                    "status": "ok",
                    "service": "battery-ai-service",
                    "model_path": str(engine.model_path),
                    "classes": engine.names,
                    "confidence_threshold": engine.confidence,
                    "image_size": engine.image_size,
                    "device": engine.device,
                },
            )

        def do_POST(self) -> None:
            endpoint = self.path.rstrip("/")
            if endpoint != "/predict":
                self.send_json(HTTPStatus.NOT_FOUND, {"status": "error", "error": "not found"})
                return
            try:
                request = json.loads(self.read_request_body().decode("utf-8"))
                image_path = Path(str(request.get("image_path", "")))
                if not str(image_path):
                    raise ValueError("image_path is required")
                response = engine.predict(image_path, request.get("inspection_id"))
                self.send_json(HTTPStatus.OK, response)
            except FileNotFoundError as exc:
                self.send_json(HTTPStatus.NOT_FOUND, {"status": "error", "error": f"Image not found: {exc}"})
            except (ValueError, json.JSONDecodeError) as exc:
                self.send_json(HTTPStatus.BAD_REQUEST, {"status": "error", "error": str(exc)})
            except Exception as exc:
                logging.exception("Inference failed")
                self.send_json(HTTPStatus.INTERNAL_SERVER_ERROR, {"status": "error", "error": str(exc)})

        def read_request_body(self) -> bytes:
            transfer_encoding = self.headers.get("Transfer-Encoding", "").lower()
            if "chunked" in transfer_encoding:
                body = bytearray()
                while True:
                    size_line = self.rfile.readline().strip().split(b";", 1)[0]
                    if not size_line:
                        raise ValueError("Invalid chunked JSON request")
                    chunk_size = int(size_line, 16)
                    if chunk_size == 0:
                        self.rfile.readline()
                        break
                    if len(body) + chunk_size > self.max_request_size:
                        raise ValueError("JSON request is too large")
                    body.extend(self.rfile.read(chunk_size))
                    self.rfile.read(2)
                if not body:
                    raise ValueError("Empty JSON request")
                return bytes(body)

            content_length = int(self.headers.get("Content-Length", "0"))
            if content_length <= 0 or content_length > self.max_request_size:
                raise ValueError("Invalid JSON request size")
            return self.rfile.read(content_length)

    return Handler


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Local YOLO service for Arduino-synchronized WPF captures."
    )
    parser.add_argument("--model", type=Path, default=Path(os.environ.get("AI_MODEL_PATH", DEFAULT_MODEL)))
    parser.add_argument("--host", default=os.environ.get("AI_HOST", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.environ.get("AI_PORT", "7100")))
    parser.add_argument("--conf", type=float, default=float(os.environ.get("AI_CONFIDENCE", "0.25")))
    parser.add_argument("--imgsz", type=int, default=int(os.environ.get("AI_IMAGE_SIZE", "640")))
    parser.add_argument("--device", default=os.environ.get("AI_DEVICE", "cpu"))
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s | %(levelname)s | %(message)s")
    logging.info("Loading model: %s", args.model)
    engine = InferenceEngine(args.model, args.conf, args.imgsz, args.device)
    server = ThreadingHTTPServer((args.host, args.port), make_handler(engine))
    logging.info("Battery AI service ready at http://%s:%d", args.host, args.port)
    logging.info("Health: http://%s:%d/health", args.host, args.port)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        logging.info("Stopping Battery AI service")
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
