BATTERY INSPECTION SYSTEM - OFFLINE WIN-X64
============================================

This package contains:
  - A self-contained .NET 8 WPF application.
  - BatteryAIService.exe with Python, Ultralytics, PyTorch and OpenCV runtime files.
  - The current YOLOv8 best.pt model.

REQUIREMENTS ON THE TARGET COMPUTER
-----------------------------------
  - 64-bit Windows 10 or Windows 11.
  - IMITECH MVS software, camera driver and required network configuration.
  - No separate .NET 8 installation is required.
  - No separate Python installation is required.

START THE COMPLETE SYSTEM
-------------------------
Option 1 (recommended): double-click WpfApp3.exe, then click START SYSTEM.
The WPF application will start the bundled AI Service automatically, connect
the configured devices and prepare the DOFBOT at HOME before starting the
conveyor.

Option 2: double-click:
  Start-BatteryInspectionSystem.cmd

The launcher starts the AI Service on http://127.0.0.1:7100, waits for its
health endpoint, and then opens the WPF interface.

START ONLY THE AI SERVICE
-------------------------
Double-click:
  Start-AI-Service.cmd

This window displays AI Service logs and should remain open while inference
is being used.

MEASURE AI SPEED ON THE MINI PC
--------------------------------
Open PowerShell in the extracted package folder and run:

  powershell -ExecutionPolicy Bypass -File .\Benchmark-AI.ps1 `
    -ImagePath "C:\path\to\an_IMITECH_image.png" -Iterations 20

The first three predictions are warm-up runs and are excluded. The script
reports internal inference time, total HTTP time, CPU and RAM, then saves all
iterations to AI_Benchmark_yyyyMMdd_HHmmss.csv.

REPLACING THE MODEL LATER
-------------------------
  1. Stop BatteryAIService.exe.
  2. Replace AIService\models\best.pt with the new model, keeping the name
     best.pt.
  3. Start the system again.

The replacement model must contain these class names:
  battery, dented, scratched (or scratch), swollen

The numeric class IDs may be different because the AI Service normalizes the
classes by name before routing the result.

CAMERA NOTE
-----------
Close image acquisition in the MVS application before starting camera capture
inside WPF. Only one application can own the IMITECH camera stream at a time.

ROBOT AND CONVEYOR NOTE
-----------------------
The target computer must still be connected to the Jetson Robot API through
the configured Ethernet/Wi-Fi network. Arduino conveyor control also requires
the correct COM port at 9600 baud.
