# DOFBOT Robot API

The authoritative Jetson backend is stored inside this project:

```text
RobotApi/Jetson/Backend/
├── robot_api.py
├── vision_trigger.py
└── deploy.ps1
```

The systemd unit and administration scripts are in `RobotApi/Jetson`. The
optional monitor-only TensorRT detector is in `RobotApi/Jetson/TensorRT`.

Deploy the current backend:

```powershell
Set-Location "D:\capstone\WPF\WpfApp3\RobotApi\Jetson\Backend"
.\deploy.ps1

Set-Location "D:\capstone\WPF\WpfApp3\RobotApi\Jetson"
.\restart_service.ps1
.\check_service.ps1
```

`robot_api.py` is the only active implementation of the robot endpoints,
including manual maintenance endpoints. Do not import the retired
`manual_control_routes.py` helper and do not run the Yahboom application while
the systemd-controlled API owns the robot and camera devices.
