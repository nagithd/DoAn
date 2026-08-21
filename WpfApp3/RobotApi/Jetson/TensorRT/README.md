# DOFBOT TensorRT monitor-only detector

This service runs the one-class YOLOv8n battery detector with the Jetson host's
TensorRT 8.0.1 runtime. It intentionally exposes inference only and cannot move
the robot. It uses the static FP16 engine at 512 x 512 and completes two CUDA
warm-up passes before opening the HTTP port, so the DOFBOT camera does not hit
the slow first-inference path.

The Robot API runs in the dedicated `dofbot_robot_api` container on port 7000.
Its camera loop opens camera index 0 (`/dev/video0`), posts JPEG frames to the
host service on port 7101 and draws returned battery boxes in
`/vision/frame.jpg`.

Install from Windows PowerShell:

```powershell
Set-Location "D:\capstone\WPF\WpfApp3\RobotApi\Jetson\TensorRT"
.\install_trt_inference.ps1
```

The installer deliberately checks `cv2`, `numpy`, and `tensorrt` before making
any systemd change. GPU buffers are managed through JetPack's existing CUDA
Runtime using Python `ctypes`, so PyCUDA is not required. If the check fails,
preserve the exact import error; do not replace JetPack's TensorRT package with
a pip wheel.

Verify the inference service without using a camera or robot:

```powershell
.\test_trt_inference.ps1 `
  -ImagePath "D:\path\to\a\dofbot-camera-sample.png" `
  -Iterations 5
```

The response must contain `monitor_only=true`. The sidecar exposes only
`GET /health` and `POST /infer`; it has no robot, conveyor, classification or
trigger endpoint.

Before enabling the camera loop, check `/dev/video0` ownership and mapping:

```powershell
.\check_dofbot_yolo_monitor.ps1 -IncludeJetsonDiagnostics
```

Close the Yahboom application and stop any other process/container that owns
`/dev/video0`. Do not run the diagnostic OpenCV camera-open test while the
Robot API camera loop is already active.

The first `dofbot_robot_api` container created for this project included only
`/dev/i2c-1`; the later attempt to add `/dev/video0` failed because the name
already existed. If diagnostics show that `/dev/video0` is missing, recreate
the container recoverably:

```powershell
.\recreate_dofbot_container_with_camera.ps1
```

This is a high-impact operation and PowerShell requests confirmation. It stops
the Robot API, renames (rather than deletes) the old container, creates the
replacement with both devices, redeploys the current source files and restarts
the service. Keep the renamed backup until the full test succeeds.

Enable the YOLO camera path with a generated, hard-locked monitor-only copy of
the project's current `vision_trigger.py`:

```powershell
.\install_dofbot_yolo_monitor.ps1
```

The generator does not edit the authoritative source file in
`RobotApi\Jetson\Backend`. It backs up the container copy, enables vision
processing and forces
`monitor_only=true` plus `auto_trigger=false` both when loading configuration
and when processing HTTP configuration updates. The install script refuses to
continue if `/dev/video0` is not mapped into `dofbot_robot_api`.

Successful status must report all of the following:

```text
vision_processing_enabled=true
monitor_only=true
auto_trigger=false
camera_open=true
detector_available=true
```

No camera detection is submitted to the robot by this monitor-only path.

To stop and disable this optional inference service when the project uses raw
DOFBOT preview only:

```powershell
.\disable_trt_inference.ps1
```

Stopping the TensorRT sidecar does not stop the Robot API. Stop camera
monitoring separately through `POST /vision/stop` or the WPF Stop button.
