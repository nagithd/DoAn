# Battery AI Service

This folder contains the complete development runtime for Windows AI
classification:

```text
Service/
├── ai_service.py
├── start_ai_service.ps1
├── models/
│   ├── best.pt
│   └── metadata.json
└── .venv/                 # local machine environment, ignored by Git
```

The selected runtime checkpoint is YOLOv8n Detection at `imgsz=640`. Its class
IDs may be in any order; the service normalizes the names `battery`, `dented`,
`scratched`, and `swollen` before routing.

WPF calls `POST /predict` after an Arduino-synchronized IMITECH capture. A
detected defect becomes one local routing result; Battery without an accepted
defect becomes `normal`; no Battery and no defect produces no route. At the
Arduino robot checkpoint WPF sends the corresponding direct `/robot/pick`
request. The DOFBOT camera remains a separate monitor-only detector.

Run the service:

```powershell
Set-Location "D:\capstone\WPF\WpfApp3\Tools\AI\Service"
.\start_ai_service.ps1
```

Override model or size when required:

```powershell
.\start_ai_service.ps1 `
  -ModelPath ".\models\best.pt" `
  -ImageSize 640 `
  -Device cpu
```

If the local environment is absent, run
`MiniPC\setup_minipc_yolo.ps1`, or set `AI_PYTHON_PATH` to another compatible
Python executable. Packaged releases use `BatteryAIService.exe` and do not
require Python on the target machine.
