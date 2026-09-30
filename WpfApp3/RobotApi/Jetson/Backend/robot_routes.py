from __future__ import annotations

import logging
import uuid
from copy import deepcopy
from typing import Any

from flask import Flask, jsonify, request

from robot_config import (
    ALLOW_UNCALIBRATED_DEFECT_CYCLES,
    CLASS_TO_BOX,
    ENABLE_MOTION,
    HOME_MOVE_TIME_MS,
    INSPECTION_CLASS_ACTIONS,
    POSES,
    VERIFY_REQUIRED_POSES_FROM_FEEDBACK,
    validate_angle,
)
from robot_jobs import enqueue_pick_job, is_worker_alive
from robot_motion import (
    is_robot_initialized,
    move_pose,
    read_servos,
    require_pose_reached,
    require_robot,
)
from robot_state import (
    get_all_jobs_copy,
    get_job_copy,
    get_state_copy,
    job_queue,
    motion_lock,
    now_iso,
    update_state,
)
from vision_trigger import VisionTriggerController, register_vision_routes
import robot_jobs as job_manager
import pick_guard


logger = logging.getLogger("dofbot-api")


def parse_manual_move_time(
    data: dict[str, Any],
    default_value: int,
) -> int:
    move_time_ms = int(data.get("move_time_ms", default_value))
    if not 100 <= move_time_ms <= 5000:
        raise ValueError(
            "move_time_ms phải nằm trong khoảng 100–5000"
        )
    return move_time_ms


def handle_vision_trigger(trigger: dict[str, Any]) -> dict[str, Any]:
    class_name = str(trigger["class_name"]).strip().lower()

    if class_name == "normal":
        event = {
            "event_id": str(uuid.uuid4()),
            "class_name": class_name,
            "action": "pass",
            "cycle": "PASS",
            "status": "completed",
            "source": "vision",
            "created_at": now_iso(),
            "inspection_id": trigger.get("inspection_id"),
            "confidence": trigger.get("confidence"),
        }
        update_state(last_routing_event=event, last_error=None)
        logger.info(
            "Normal battery routed to PASS: inspection_id=%s confidence=%s",
            event["inspection_id"],
            event["confidence"],
        )
        return event

    wrist_value = trigger.get("wrist_angle")
    wrist_angle = (
        None
        if wrist_value is None
        else int(round(float(wrist_value)))
    )
    return enqueue_pick_job(
        class_name=class_name,
        wrist_angle=wrist_angle,
        start_delay_ms=int(trigger["start_delay_ms"]),
        prepositioned=True,
        source="vision",
    )


def register_routes(app: Flask) -> VisionTriggerController:
    vision_controller = VisionTriggerController(
        on_trigger=handle_vision_trigger,
        get_robot_state=get_state_copy,
    )
    register_vision_routes(app, vision_controller)
    job_manager.vision_snapshot = vision_controller.status

    @app.get("/health")
    def health():
        state = get_state_copy()
        return jsonify({
            "status": "ok",
            "conveyor_handshake_version": 1,
            "pick_guard_enabled": pick_guard.ENABLED,
            "service": "dofbot-robot-api",
            "motion_enabled": ENABLE_MOTION,
            "robot_initialized": is_robot_initialized(),
            "worker_alive": is_worker_alive(),
            "queue_size": job_queue.qsize(),
            "servo_feedback_available": state.get(
                "servo_feedback_available"
            ),
            "pose_confirmation": state.get("pose_confirmation"),
            "pose_verification_mode": (
                "servo_feedback"
                if VERIFY_REQUIRED_POSES_FROM_FEEDBACK
                else "commanded_state"
            ),
            "vision_available": True,
            "inspection_classes": sorted(
                INSPECTION_CLASS_ACTIONS.keys()
            ),
            "time": now_iso(),
        })

    @app.get("/robot/status")
    def robot_status():
        return jsonify({"success": True, "robot": get_state_copy()})

    @app.get("/robot/poses")
    def robot_poses():
        return jsonify({
            "success": True,
            "poses": deepcopy(POSES),
            "class_to_box": deepcopy(CLASS_TO_BOX),
            "inspection_class_actions": deepcopy(
                INSPECTION_CLASS_ACTIONS
            ),
            "uncalibrated_cycles_allowed": (
                ALLOW_UNCALIBRATED_DEFECT_CYCLES
            ),
            "pose_verification_mode": (
                "servo_feedback"
                if VERIFY_REQUIRED_POSES_FROM_FEEDBACK
                else "commanded_state"
            ),
        })

    @app.get("/robot/routing")
    def robot_routing():
        return jsonify({
            "success": True,
            "inspection_class_actions": deepcopy(
                INSPECTION_CLASS_ACTIONS
            ),
            "defect_cycles": deepcopy(CLASS_TO_BOX),
            "last_routing_event": get_state_copy().get(
                "last_routing_event"
            ),
        })

    @app.get("/robot/servos")
    def robot_servos():
        state = get_state_copy()
        if state["busy"] or not motion_lock.acquire(blocking=False):
            return jsonify({
                "success": False,
                "error": (
                    "Servo feedback is unavailable while robot is moving"
                ),
                "robot": state,
            }), 409

        try:
            values = read_servos()
            return jsonify({
                "success": True,
                "servos": values,
                "feedback_available": all(
                    isinstance(value, (int, float)) for value in values
                ),
                "time": now_iso(),
            })
        except Exception as exc:
            logger.exception("Không thể đọc servo")
            return jsonify({"success": False, "error": str(exc)}), 500
        finally:
            motion_lock.release()

    @app.post("/robot/servo")
    def robot_set_servo():
        if not ENABLE_MOTION:
            return jsonify({
                "success": False,
                "error": "ENABLE_MOTION đang False",
            }), 409

        data = request.get_json(silent=True) or {}
        try:
            servo_id = int(data.get("servo_id"))
            angle = int(round(float(data.get("angle"))))
            move_time_ms = parse_manual_move_time(data, 500)
            validate_angle(angle)
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

        if get_state_copy()["busy"] or not motion_lock.acquire(blocking=False):
            return jsonify({
                "success": False,
                "error": "Robot đang bận",
                "robot": get_state_copy(),
            }), 409

        try:
            if get_state_copy()["busy"]:
                return jsonify({"success": False, "error": "Robot has a reserved job"}), 409
            robot = require_robot()
            update_state(
                state="manual_control",
                busy=True,
                current_job_id=None,
                current_class=None,
                current_step=f"SERVO_{servo_id}",
                last_error=None,
            )
            logger.info(
                "MANUAL servo=%s angle=%s time_ms=%s",
                servo_id,
                angle,
                move_time_ms,
            )
            robot.Arm_serial_servo_write(
                servo_id,
                angle,
                move_time_ms,
            )
            update_state(
                state="idle",
                busy=False,
                current_step=None,
                commanded_pose_name=None,
                commanded_pose=None,
                pose_confirmation="manual_command",
                last_error=None,
            )
            return jsonify({
                "success": True,
                "servo_id": servo_id,
                "target_angle": angle,
                "accepted": True,
            })
        except Exception as exc:
            logger.exception(
                "Điều khiển servo %s thất bại",
                servo_id,
            )
            update_state(
                state="error",
                busy=False,
                current_step=None,
                last_error=str(exc),
            )
            return jsonify({"success": False, "error": str(exc)}), 500
        finally:
            motion_lock.release()

    @app.post("/robot/pick")
    def robot_pick():
        data = request.get_json(silent=True) or {}
        class_name = str(data.get("class_name", "")).strip().lower()
        requested_job_id = str(data.get("job_id", "")).strip()
        wrist_value = data.get("wrist_angle")

        try:
            source = str(data.get("source", "api"))
            if source not in {"api", "arduino_checkpoint"}:
                raise ValueError("Unsupported job source")
            wrist_angle = (
                None
                if wrist_value is None
                else int(round(float(wrist_value)))
            )
            start_delay_ms = int(data.get("start_delay_ms", 0))
            prepositioned_value = data.get("prepositioned", False)
            if not isinstance(prepositioned_value, bool):
                raise ValueError(
                    "prepositioned phải là true hoặc false"
                )

            if source == "arduino_checkpoint" and (
                not prepositioned_value or start_delay_ms != 0 or wrist_angle is not None
            ):
                raise ValueError("Checkpoint handshake currently supports calibrated fixed poses only")

            job = enqueue_pick_job(
                class_name=class_name,
                requested_job_id=requested_job_id,
                wrist_angle=wrist_angle,
                start_delay_ms=start_delay_ms,
                prepositioned=prepositioned_value,
                source=source,
            )
        except (KeyError, RuntimeError) as exc:
            return jsonify({"success": False, "error": str(exc)}), 409
        except (TypeError, ValueError) as exc:
            return jsonify({
                "success": False,
                "error": str(exc),
                "allowed_classes": sorted(CLASS_TO_BOX.keys()),
            }), 400

        return jsonify({
            "success": True,
            "accepted": True,
            "job": job,
            "queue_size": job_queue.qsize(),
        }), 202

    @app.get("/robot/jobs/<job_id>")
    def robot_job(job_id: str):
        job = get_job_copy(job_id)
        if job is None:
            return jsonify({
                "success": False,
                "error": "Không tìm thấy job",
            }), 404
        return jsonify({"success": True, "job": job})

    @app.get("/robot/jobs")
    def robot_jobs():
        all_jobs = get_all_jobs_copy()
        return jsonify({
            "success": True,
            "jobs": all_jobs,
            "count": len(all_jobs),
        })

    @app.post("/robot/home")
    def robot_home():
        state = get_state_copy()
        if state["busy"] or not motion_lock.acquire(blocking=False):
            return jsonify({
                "success": False,
                "error": "Robot đang bận",
                "robot": state,
            }), 409

        try:
            if get_state_copy()["busy"]:
                return jsonify({"success": False, "error": "Robot has a reserved job"}), 409
            update_state(
                state="homing",
                busy=True,
                current_step="HOME",
                last_error=None,
            )
            move_pose("HOME", HOME_MOVE_TIME_MS)
            home_confirmation = require_pose_reached("HOME")
            update_state(
                state="vision_ready",
                busy=False,
                current_step=None,
                last_error=None,
            )
            return jsonify({
                "success": True,
                "state": "vision_ready",
                "pose": list(POSES["HOME"]),
                "pose_confirmation": home_confirmation,
            })
        except Exception as exc:
            logger.exception("Lệnh HOME thất bại")
            update_state(
                state="error",
                busy=False,
                current_step=None,
                last_error=str(exc),
            )
            return jsonify({"success": False, "error": str(exc)}), 500
        finally:
            motion_lock.release()

    @app.post("/robot/reset")
    def robot_reset():
        state = get_state_copy()
        if state["busy"] or not motion_lock.acquire(blocking=False):
            return jsonify({
                "success": False,
                "error": "Robot đang bận",
                "robot": state,
            }), 409

        try:
            if get_state_copy()["busy"]:
                return jsonify({"success": False, "error": "Robot has a reserved job"}), 409
            update_state(
                state="resetting",
                busy=True,
                current_step="HOME",
                last_error=None,
            )
            move_pose("HOME", HOME_MOVE_TIME_MS)
            home_confirmation = require_pose_reached("HOME")
            update_state(
                state="vision_ready",
                busy=False,
                current_job_id=None,
                current_class=None,
                current_step=None,
                last_error=None,
            )
            return jsonify({
                "success": True,
                "state": "vision_ready",
                "pose_confirmation": home_confirmation,
            })
        except Exception as exc:
            logger.exception("RESET command failed")
            update_state(
                state="error",
                busy=False,
                current_job_id=None,
                current_class=None,
                current_step=None,
                last_error=str(exc),
            )
            return jsonify({"success": False, "error": str(exc)}), 500
        finally:
            motion_lock.release()

    @app.errorhandler(404)
    def not_found(_error):
        return jsonify({
            "success": False,
            "error": "Endpoint không tồn tại",
        }), 404

    @app.errorhandler(405)
    def method_not_allowed(_error):
        return jsonify({
            "success": False,
            "error": "HTTP method không được hỗ trợ",
        }), 405

    @app.errorhandler(500)
    def internal_error(error):
        logger.exception("Lỗi HTTP nội bộ: %s", error)
        return jsonify({
            "success": False,
            "error": "Lỗi nội bộ Robot API",
        }), 500

    return vision_controller
