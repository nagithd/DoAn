BATTERY AI SERVICE - OFFLINE WIN-X64
====================================

PURPOSE
-------
This package tests the YOLO battery-inspection component independently from
the WPF interface, IMITECH camera, Arduino conveyor and DOFBOT Robot API.

TARGET COMPUTER REQUIREMENTS
----------------------------
  - 64-bit Windows 10 or Windows 11.
  - No Python installation is required.
  - No .NET installation is required.
  - Internet access is not required.
  - MVS is not required when testing the AI Service with existing image files.

START THE SERVICE
-----------------
Double-click Start-AI-Service.cmd and keep its console window open.

The first model load may take several seconds. The service listens at:
  http://127.0.0.1:7100

CHECK SERVICE STATUS
--------------------
After the service reports that it is ready, double-click Test-AI-Health.cmd.
The expected status is "ok", and the class mapping should be:
  0 = dented
  1 = battery
  2 = scratched
  3 = swollen

STOP THE SERVICE
----------------
Close the Battery AI Service console window or press Ctrl+C inside it.

REPLACE THE MODEL
-----------------
Stop the service and replace AIService\models\best.pt. Keep the same filename
and class mapping, then start the service again.

CONTENTS
--------
  AIService\BatteryAIService.exe  Packaged local API executable
  AIService\_internal\            Python/Ultralytics/PyTorch/OpenCV runtime
  AIService\models\best.pt        Current YOLOv8 model
  Start-AI-Service.cmd             Service launcher
  Test-AI-Health.cmd               Local health test

