# AI service packaging

`BatteryAIService.spec` is the retained PyInstaller recipe for the local
YOLOv8 HTTP service.  Generated PyInstaller work and distribution folders
belong under the ignored `artifacts` directory, not beside the WPF source.

From the `WpfApp3` project directory, activate the intended AI environment
and run:

```powershell
pyinstaller --clean --noconfirm `
  --workpath artifacts/ai-pyinstaller-build `
  --distpath artifacts/ai-pyinstaller-dist `
  Tools/AI/Packaging/BatteryAIService.spec
```

Copy the explicitly selected model checkpoint into the packaged
`models/best.pt` location only after recording its version and checksum.
The dated 2026-08-05 offline archive contains the older v2 detector and must
not be described as the current segmentation candidate.
