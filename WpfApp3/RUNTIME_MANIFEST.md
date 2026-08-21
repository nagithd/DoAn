# Runtime source manifest

Updated: 2026-08-20

## WPF application

- Project: `WpfApp3.csproj`
- Windows target: .NET 8, x64
- External vendor dependency: MVS 64-bit .NET SDK (`MvCameraControl.Net.dll`)

## Windows AI service

- Source: `Tools/AI/Service/ai_service.py`
- Starter: `Tools/AI/Service/start_ai_service.ps1`
- Model: `Tools/AI/Service/models/best.pt`
- Model: YOLOv8n Detection, 640 px, four classes
- SHA-256: `50DD7D69BAD1CAEE3C20D41D3E921B950E457BD50DD7283984D37CF1C669F03B`

## Jetson Robot API

- Backend: `RobotApi/Jetson/Backend`
- systemd: `RobotApi/Jetson/dofbot-robot-api.service`
- Robot API SHA-256: `3A03861EB663BBA96CC97204BAEF9447A0EE8BD370EF0FBDB4182B5795B92898`
- Vision SHA-256: `4FD471B7D4828C608526FDBEEB981F9B2FE311C7AFE41EA0B9B14B42FBA3447D`

## DOFBOT TensorRT monitor

- Source: `RobotApi/Jetson/TensorRT`
- Portable model: `models/dofbot_battery_yolov8n_512_opset12.onnx`
- ONNX SHA-256: `5E7081181ABA42A1B510C50F05EC3AD585EA36011A6B530D0EBEC679AC19D30D`
- The Jetson-generated FP16 `.engine` remains hardware-specific and is not
  copied into the Windows source tree.

Generated releases, training runs, datasets, reports, backups, and temporary
files are stored under `D:\capstone\WpfApp3_NonRuntime`.

