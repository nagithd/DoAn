# DOFBOT camera entry trigger

This module implements the first safe version of the conveyor-entry design:

```text
VISION_HOME
-> object enters the configured camera zone
-> estimate object angle relative to conveyor direction
-> calculate Servo 5 angle and start delay
-> enqueue the existing fixed-pose pick sequence
-> return to VISION_HOME
```

## Safety defaults

- `auto_trigger` is `false` by default.
- Starting the camera does not move the robot.
- Automatic jobs are accepted only when Robot API state is `vision_ready`.
- Late triggers can be rejected instead of moving after the object has already
  passed the target.
- Servo 5 correction is limited by `max_wrist_correction_deg` and the configured
  wrist angle limits.

## Files

- `/root/robot_api.py`: robot poses, queue and motion endpoints.
- `/root/vision_trigger.py`: camera thread, entry detection and timing.
- `/root/vision_config.json`: created after the first configuration update.

The Windows `deploy.ps1` script uploads and checks both Python files.

## New Robot API features

### Move to the observation pose

```http
POST /robot/vision-home
```

This moves the robot to `VISION_HOME`, opens the gripper using the pose value
and changes Robot API state to `vision_ready`.

### Queue a timed fixed-position pick

```http
POST /robot/pick
Content-Type: application/json
```

Example:

```json
{
  "class_name": "metal",
  "wrist_angle": 105,
  "start_delay_ms": 500,
  "prepositioned": true
}
```

- `wrist_angle` changes only Servo 5 in `PICK_ABOVE`, `PICK_DOWN` and
  `PICK_LIFT`.
- `start_delay_ms` waits before the robot starts moving.
- `prepositioned=true` skips the redundant HOME and open-gripper actions.

## Vision endpoints

- `GET /vision/status`
- `GET /vision/config`
- `POST /vision/config`
- `POST /vision/start`
- `POST /vision/stop`
- `POST /vision/reset-trigger`
- `GET /vision/frame.jpg`

## First deployment

Stop the manually running API, then run on Windows:

```powershell
cd D:\capstone\Dofbot
.\deploy.ps1
.\run_api.ps1
```

Check health:

```powershell
Invoke-RestMethod http://192.168.137.179:7000/health
```

The response should contain:

```text
vision_available : True
```

## Safe calibration sequence

### 1. Keep automatic motion disabled

```powershell
$config = @{
    auto_trigger = $false
    camera_index = 0
    entry_edge = "top"
    rail_left_ratio = 0.15
    rail_right_ratio = 0.85
    entry_zone_ratio = 0.28
} | ConvertTo-Json

Invoke-RestMethod `
    -Method Post `
    -Uri http://192.168.137.179:7000/vision/config `
    -ContentType application/json `
    -Body $config
```

### 2. Move to VISION_HOME

Keep the physical power switch within reach, then:

```powershell
Invoke-RestMethod `
    -Method Post `
    -Uri http://192.168.137.179:7000/robot/vision-home
```

Adjust the `VISION_HOME` pose in `robot_api.py` until the camera is stable and
looks at the conveyor entry.

### 3. Start camera monitoring

```powershell
Invoke-RestMethod `
    -Method Post `
    -Uri http://192.168.137.179:7000/vision/start
```

Open this address in a browser:

```text
http://192.168.137.179:7000/vision/frame.jpg
```

The image shows:

- blue lines: conveyor guide limits;
- yellow rectangle: entry detection zone;
- green rectangle: detected object;
- relative object angle and proposed wrist angle;
- `MONITOR ONLY`: motion is disabled.

`frame.jpg` is a snapshot endpoint. Refresh the page to obtain a newer frame.

### 4. Tune detection without moving the robot

Inspect:

```powershell
Invoke-RestMethod http://192.168.137.179:7000/vision/status |
    ConvertTo-Json -Depth 8
```

Tune these fields with `POST /vision/config`:

- `rail_left_ratio`, `rail_right_ratio`
- `entry_edge`, `entry_zone_ratio`
- `min_contour_area`
- `stable_frames`
- `conveyor_angle_deg`
- `wrist_reference_angle`
- `wrist_direction`
- `max_wrist_correction_deg`

If camera index, resolution or background settings change, stop and start the
vision camera again.

### 5. Measure timing

Required values:

- `belt_speed_mm_s`
- `distance_to_pick_mm`
- `robot_time_to_grip_ms`
- `processing_margin_ms`

The controller calculates:

```text
arrival_ms = distance_to_pick_mm / belt_speed_mm_s * 1000

start_delay_ms =
    arrival_ms
    - robot_time_to_grip_ms
    - processing_margin_ms
```

If the result is negative, the status reports `late=true`. Move the camera
upstream, reduce belt speed or stop the conveyor.

### 6. Enable automatic trigger last

Only after monitor-only detection, wrist angle and timing are verified:

```powershell
$config = @{
    auto_trigger = $true
    default_class = "metal"
    reject_late_trigger = $true
} | ConvertTo-Json

Invoke-RestMethod `
    -Method Post `
    -Uri http://192.168.137.179:7000/vision/config `
    -ContentType application/json `
    -Body $config
```

Keep one object on the conveyor per cycle during the first tests.

## Important limitations of this first version

- Moving-object detection uses a stationary-camera background model.
- The camera must be at `VISION_HOME` for automatic trigger.
- The two conveyor rails define the corridor but are configured as image
  ratios; automatic rail-line calibration is not implemented yet.
- AI classification is not connected yet. `default_class` selects the fixed
  destination box for calibration.
- Conveyor stop/run control is not connected yet.
- Actual parameters must be tuned using a real camera frame.
