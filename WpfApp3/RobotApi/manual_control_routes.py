"""Manual-servo endpoints for the existing DOFBOT robot_api.py.

Copy this file next to /root/robot_api.py in the kind_pare container, import
register_manual_control_routes, then register the routes before app.run().
"""

import time

from flask import jsonify, request


def register_manual_control_routes(
    app,
    arm,
    robot_lock,
    robot_state,
    update_state,
    motion_enabled,
):
    def read_servos():
        return [
            arm.Arm_serial_servo_read(servo_id)
            for servo_id in range(1, 7)
        ]

    def parse_move_time(data, default_value):
        move_time_ms = int(data.get("move_time_ms", default_value))
        if not 100 <= move_time_ms <= 5000:
            raise ValueError("move_time_ms phải nằm trong khoảng 100–5000")
        return move_time_ms

    @app.post("/robot/servo")
    def set_single_servo():
        if not motion_enabled:
            return jsonify({
                "success": False,
                "error": "ENABLE_MOTION đang False",
            }), 409

        data = request.get_json(silent=True) or {}

        try:
            servo_id = int(data.get("servo_id"))
            angle = float(data.get("angle"))
            move_time_ms = parse_move_time(data, 500)
        except (TypeError, ValueError) as exc:
            return jsonify({
                "success": False,
                "error": f"Dữ liệu điều khiển không hợp lệ: {exc}",
            }), 400

        if not 1 <= servo_id <= 6:
            return jsonify({
                "success": False,
                "error": "servo_id phải nằm trong khoảng 1–6",
            }), 400

        if not 0 <= angle <= 180:
            return jsonify({
                "success": False,
                "error": "angle phải nằm trong khoảng 0–180",
            }), 400

        if not robot_lock.acquire(blocking=False):
            return jsonify({
                "success": False,
                "error": "Robot đang bận",
                "robot_state": robot_state,
            }), 409

        try:
            update_state(
                state="manual",
                busy=True,
                current_step=f"SERVO_{servo_id}",
                last_error=None,
            )

            arm.Arm_serial_servo_write(
                servo_id,
                int(round(angle)),
                move_time_ms,
            )
            time.sleep(move_time_ms / 1000.0 + 0.15)

            actual_angle = arm.Arm_serial_servo_read(servo_id)
            update_state(
                state="idle",
                busy=False,
                current_step=None,
            )

            return jsonify({
                "success": True,
                "servo_id": servo_id,
                "target_angle": angle,
                "actual_angle": actual_angle,
            })
        except Exception as exc:
            update_state(
                state="error",
                busy=False,
                current_step=None,
                last_error=str(exc),
            )
            return jsonify({
                "success": False,
                "error": str(exc),
            }), 500
        finally:
            robot_lock.release()

    @app.post("/robot/servos")
    def set_all_servos():
        if not motion_enabled:
            return jsonify({
                "success": False,
                "error": "ENABLE_MOTION đang False",
            }), 409

        data = request.get_json(silent=True) or {}
        angles = data.get("angles")

        if not isinstance(angles, list) or len(angles) != 6:
            return jsonify({
                "success": False,
                "error": "angles phải là danh sách gồm đúng 6 góc",
            }), 400

        try:
            angles = [float(angle) for angle in angles]
            move_time_ms = parse_move_time(data, 1000)
        except (TypeError, ValueError) as exc:
            return jsonify({
                "success": False,
                "error": f"Dữ liệu điều khiển không hợp lệ: {exc}",
            }), 400

        if any(angle < 0 or angle > 180 for angle in angles):
            return jsonify({
                "success": False,
                "error": "Mỗi góc phải nằm trong khoảng 0–180",
            }), 400

        if not robot_lock.acquire(blocking=False):
            return jsonify({
                "success": False,
                "error": "Robot đang bận",
                "robot_state": robot_state,
            }), 409

        try:
            update_state(
                state="manual",
                busy=True,
                current_step="ALL_SERVOS",
                last_error=None,
            )

            rounded_angles = [int(round(angle)) for angle in angles]
            arm.Arm_serial_servo_write6_array(
                rounded_angles,
                move_time_ms,
            )
            time.sleep(move_time_ms / 1000.0 + 0.2)

            actual_angles = read_servos()
            update_state(
                state="idle",
                busy=False,
                current_step=None,
            )

            return jsonify({
                "success": True,
                "target_angles": angles,
                "servos": actual_angles,
            })
        except Exception as exc:
            update_state(
                state="error",
                busy=False,
                current_step=None,
                last_error=str(exc),
            )
            return jsonify({
                "success": False,
                "error": str(exc),
            }), 500
        finally:
            robot_lock.release()
