# DOFBOT Robot API systemd service

This service matches the current deployment architecture:

- Jetson login: `jetson`
- Docker container: `kind_pare`
- API inside container: `/root/robot_api.py`
- Vision module inside container: `/root/vision_trigger.py`
- HTTP port: `7000`

## Install from Windows

First deploy the latest Python files using the existing
`D:\capstone\Dofbot\deploy.ps1`.

Then run:

```powershell
Set-Location D:\capstone\WPF\WpfApp3\RobotApi\Jetson
.\install_service.ps1
```

The script uploads the unit, installs it in `/etc/systemd/system`, enables
it at boot and starts it immediately. SSH may ask for the Jetson login
password and `sudo` may ask for it again.

The service allows up to 300 seconds for Docker and the existing
`kind_pare` container to become ready during a cold boot. It also passes
`TZ=Asia/Ho_Chi_Minh` into the API process so Python and systemd logs use
the same timezone.

## Check status and logs

```powershell
.\check_service.ps1
```

The check waits up to 300 seconds for `/health` before reporting a
failure. To perform an immediate check without waiting:

```powershell
.\check_service.ps1 -WaitSeconds 0
```

Or directly on Jetson:

```bash
sudo systemctl status dofbot-robot-api.service
sudo journalctl -u dofbot-robot-api.service -f
curl http://127.0.0.1:7000/health
```

## Restart after deploying new Python files

```powershell
.\restart_service.ps1
```

## Stop or disable

```bash
sudo systemctl stop dofbot-robot-api.service
sudo systemctl disable dofbot-robot-api.service
```

## Important

Do not run `run_api.ps1` at the same time as this service. The unit stops
an older manually launched `robot_api.py` process before starting its own
tracked process.

The current Python configuration has `ENABLE_MOTION = True`. Perform the
first boot test with the robot workspace clear, or temporarily set it to
`False` before deploying if the calibrated poses have not yet been
verified.
