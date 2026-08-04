# DOFBOT manual-control API

The WPF `Robot Manual` tab uses the existing read endpoints:

- `GET /health`
- `GET /robot/status`
- `GET /robot/servos`

It additionally requires:

- `POST /robot/servo` to move one servo.
- `POST /robot/servos` to move all six servos.

Copy `manual_control_routes.py` to `/root` inside the `kind_pare` container:

```bash
docker cp manual_control_routes.py kind_pare:/root/manual_control_routes.py
```

In `/root/robot_api.py`, import the helper near the other imports:

```python
from manual_control_routes import register_manual_control_routes
```

Then, inside `if __name__ == "__main__":`, register the routes immediately
after `initialize_robot()` and before `app.run(...)`:

```python
initialize_robot()
register_manual_control_routes(
    app=app,
    arm=arm,
    robot_lock=motion_lock,
    robot_state=robot_state,
    update_state=update_state,
    motion_enabled=ENABLE_MOTION,
)
```

It is important to call this after `initialize_robot()` so that `arm` is an
`Arm_Device`, not `None`. Restart the Robot API after editing.

Start with a long movement time and move only one servo by a few degrees. The
software Reset command is not an emergency stop; keep the physical power switch
within reach while calibrating.
