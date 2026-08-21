# Mini PC YOLO CPU Setup

This setup is intended for the project mini PC with 64-bit Windows 10, an
AMD GX-212JC CPU and 8 GB RAM. It runs inference only. Do not train a model on
this computer.

## Required layout

Keep the following structure after copying `Tools/AI/Service` to the mini PC:

```text
Service/
|-- ai_service.py
|-- models/
|   `-- best.pt
`-- MiniPC/
    |-- setup_minipc_yolo.ps1
    |-- Start-MiniPC-AI-Service.ps1
    `-- Test-MiniPC-AI.ps1
```

## One-time installation

1. Install Python 3.11 x64 from python.org. Enable the Python launcher during
   installation.
2. Open PowerShell in the `MiniPC` directory.
3. Run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\setup_minipc_yolo.ps1
```

The package download requires Internet access. The resulting environment is
stored in `Service\.venv-minipc` and does not modify the system Python
packages.

## Start the service

Copy the detector checkpoint to `Service\models\best.pt`, then run:

```powershell
.\Start-MiniPC-AI-Service.ps1
```

Keep the PowerShell window open. In another window run:

```powershell
.\Test-MiniPC-AI.ps1
```

The expected health address is `http://127.0.0.1:7100/health`, the device is
`cpu`, and CUDA is `False`.

## Low-resource defaults

- CPU threads: 2
- inference image size: 512
- service bind address: localhost only
- model: use the YOLOv8n detection checkpoint, not YOLOv8m segmentation

If CPU use is still too high, start with `-ImageSize 416 -CpuThreads 1`. This
reduces load but may also reduce small-defect accuracy. Measure both inference
time and accuracy before adopting the lower setting.
