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
Double-click:
  Start-BatteryInspectionSystem.cmd

The launcher starts the AI Service on http://127.0.0.1:7100, waits for its
health endpoint, and then opens the WPF interface.

START ONLY THE AI SERVICE
-------------------------
Double-click:
  Start-AI-Service.cmd

This window displays AI Service logs and should remain open while inference
is being used.

REPLACING THE MODEL LATER
-------------------------
  1. Stop BatteryAIService.exe.
  2. Replace AIService\models\best.pt with the new model, keeping the name
     best.pt.
  3. Start the system again.

The replacement model must retain this class mapping:
  0 = dented
  1 = battery
  2 = scratched
  3 = swollen

CAMERA NOTE
-----------
Close image acquisition in the MVS application before starting camera capture
inside WPF. Only one application can own the IMITECH camera stream at a time.

ROBOT AND CONVEYOR NOTE
-----------------------
The target computer must still be connected to the Jetson Robot API through
the configured Ethernet/Wi-Fi network. Arduino conveyor control also requires
the correct COM port at 9600 baud.

