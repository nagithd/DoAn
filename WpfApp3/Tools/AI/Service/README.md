# Battery AI Service

This local Windows service loads the YOLO checkpoint once and accepts images
captured by the WPF Capture Zone.

## Start

```powershell
Set-Location "D:\capstone\WPF\WpfApp3\Tools\AI\Service"
.\start_ai_service.ps1
```

Health endpoint: `http://127.0.0.1:7100/health`

The service exposes two inference endpoints:

- `POST /detect-zone`: lightweight YOLO battery-only detection on the centred ROI.
- `POST /predict`: full-resolution battery and defect inspection after the zone latches.

The WPF application sends this request after an automatic capture:

```json
{
  "image_path": "C:\\path\\to\\captured-image.bmp",
  "inspection_id": "unique-id"
}
```

## Replace the model

Stop the service and start it with another compatible checkpoint:

```powershell
.\start_ai_service.ps1 -ModelPath "D:\models\new-best.pt"
```

The replacement model must expose exactly these classes:

```text
0=dented, 1=battery, 2=scratched, 3=swollen
```

The service maps a battery with no detected defect to `normal`. If several
defects are present, the highest-confidence defect is the temporary routing
result. The service does not enqueue robot jobs; that remains disabled during
Capture Zone commissioning.

The WPF Capture Zone checks a resized JPEG ROI at approximately 5 Hz. Two
consecutive battery detections latch the zone; three consecutive clear results
rearm it. This replaces background subtraction, so belt texture and lighting
changes do not trigger a capture unless YOLO identifies a battery.
