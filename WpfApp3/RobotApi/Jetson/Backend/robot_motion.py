from __future__ import annotations

import logging
import time
from copy import deepcopy
from typing import Any

from Arm_Lib import Arm_Device

from robot_config import (
    ALLOW_COMMANDED_POSE_FALLBACK,
    DEFAULT_MOVE_TIME_MS,
    ENABLE_MOTION,
    GRIPPER_MOVE_TIME_MS,
    HOME_MOVE_TIME_MS,
    POSES,
    REQUIRED_POSE_VERIFY_ATTEMPTS,
    REQUIRED_POSE_VERIFY_DELAY_SECONDS,
    SERVO_TOLERANCE_DEGREES,
    STRICT_SERVO_VERIFICATION,
    VERIFY_POSE_AFTER_MOVE,
    VERIFY_REQUIRED_POSES_FROM_FEEDBACK,
    WAIT_AFTER_GRIP_SECONDS,
    WAIT_AFTER_MOVE_SECONDS,
    validate_angle,
    validate_pose,
)
from robot_state import get_state_copy, now_iso, update_state


logger = logging.getLogger("dofbot-api")
arm: Arm_Device | None = None


def initialize_arm() -> None:
    global arm
    arm = Arm_Device()


def is_robot_initialized() -> bool:
    return arm is not None


def require_robot() -> Arm_Device:
    if arm is None:
        raise RuntimeError("Arm_Device chưa được khởi tạo")
    return arm


def read_servos() -> list[Any]:
    robot = require_robot()
    values: list[Any] = []

    for servo_id in range(1, 7):
        try:
            value = robot.Arm_serial_servo_read(servo_id)
        except Exception as exc:
            logger.exception("Không đọc được servo %s", servo_id)
            value = {"error": str(exc)}
        values.append(value)

    readable_count = sum(
        isinstance(value, (int, float)) for value in values
    )
    update_state(
        servo_feedback_available=(readable_count == 6),
        last_servo_feedback=deepcopy(values),
        last_servo_feedback_at=now_iso(),
    )
    return values


def inspect_pose(
    target_pose: list[int],
    tolerance: int = SERVO_TOLERANCE_DEGREES,
) -> tuple[str, list[Any], list[str]]:
    """Return verified, mismatch or unavailable for one pose readback."""
    current = read_servos()
    unreadable: list[str] = []
    mismatches: list[str] = []

    for index, target in enumerate(target_pose):
        servo_id = index + 1
        actual = current[index]

        if not isinstance(actual, (int, float)):
            unreadable.append(
                f"servo {servo_id}: khong doc duoc gia tri"
            )
            continue

        difference = abs(actual - target)
        if difference > tolerance:
            mismatches.append(
                f"servo {servo_id}: target={target}, "
                f"actual={actual}, lech={difference}"
            )

    if mismatches:
        return "mismatch", current, mismatches + unreadable
    if unreadable:
        return "unavailable", current, unreadable
    return "verified", current, []


def verify_pose(
    target_pose: list[int],
    tolerance: int = SERVO_TOLERANCE_DEGREES,
) -> bool:
    result, _current, errors = inspect_pose(target_pose, tolerance)

    if result != "verified":
        message = "; ".join(errors)
        logger.warning("Pose verification %s: %s", result, message)
        if STRICT_SERVO_VERIFICATION:
            raise RuntimeError(message)
        return False
    return True


def require_pose_reached(
    pose_name: str,
    attempts: int = REQUIRED_POSE_VERIFY_ATTEMPTS,
) -> str:
    """Confirm a critical pose, with an explicit commanded-pose fallback."""
    if not ENABLE_MOTION:
        return "motion_disabled"
    if pose_name not in POSES:
        raise KeyError(f"Khong ton tai pose: {pose_name}")

    target_pose = list(POSES[pose_name])

    if not VERIFY_REQUIRED_POSES_FROM_FEEDBACK:
        state = get_state_copy()
        if (
            state.get("commanded_pose_name") == pose_name
            and state.get("commanded_pose") == target_pose
        ):
            update_state(pose_confirmation="commanded_only")
            logger.warning(
                "Critical pose %s accepted from command state; automatic "
                "servo feedback is disabled (open-loop)",
                pose_name,
            )
            return "commanded_only"

        raise RuntimeError(
            f"Robot has no matching command history for required pose "
            f"{pose_name}"
        )

    last_result = "unknown"
    last_errors: list[str] = []

    for attempt in range(1, attempts + 1):
        result, _current, errors = inspect_pose(target_pose)
        last_result = result
        last_errors = errors

        if result == "verified":
            update_state(pose_confirmation="verified")
            logger.info(
                "Critical pose verified: %s (attempt %s/%s)",
                pose_name,
                attempt,
                attempts,
            )
            return "verified"

        if result == "unavailable":
            state = get_state_copy()
            commanded_name = state.get("commanded_pose_name")
            commanded_pose = state.get("commanded_pose")

            if (
                ALLOW_COMMANDED_POSE_FALLBACK
                and commanded_name == pose_name
                and commanded_pose == target_pose
            ):
                update_state(pose_confirmation="commanded_only")
                logger.warning(
                    "Servo feedback unavailable for %s; accepting the most "
                    "recent successfully submitted pose command (open-loop)",
                    pose_name,
                )
                return "commanded_only"
            break

        if attempt < attempts:
            time.sleep(REQUIRED_POSE_VERIFY_DELAY_SECONDS)

    detail = "; ".join(last_errors) or last_result
    update_state(pose_confirmation=last_result)
    raise RuntimeError(
        f"Robot pose {pose_name} confirmation failed: "
        f"result={last_result}; {detail}"
    )


def move_pose(
    pose_name: str,
    move_time_ms: int = DEFAULT_MOVE_TIME_MS,
    wrist_angle: int | None = None,
    gripper_angle: int | None = None,
) -> None:
    robot = require_robot()

    if pose_name not in POSES:
        raise KeyError(f"Không tồn tại pose: {pose_name}")

    pose = list(POSES[pose_name])

    if wrist_angle is not None:
        validate_angle(wrist_angle)
        pose[4] = int(round(wrist_angle))

    # Chỉ ghi đè bản sao cục bộ để pose ABOVE vẫn đóng khi mang pin tới hộp,
    # nhưng giữ kẹp mở khi rút tay sau RELEASE_OBJECT.
    if gripper_angle is not None:
        validate_angle(gripper_angle)
        pose[5] = int(round(gripper_angle))

    validate_pose(pose_name, pose)
    update_state(current_step=pose_name)

    logger.info(
        "MOVE pose=%s angles=%s time_ms=%s enabled=%s",
        pose_name,
        pose,
        move_time_ms,
        ENABLE_MOTION,
    )

    if ENABLE_MOTION:
        robot.Arm_serial_servo_write6_array(pose, move_time_ms)
        update_state(
            commanded_pose_name=pose_name,
            commanded_pose=deepcopy(pose),
            pose_confirmation="commanded",
        )

    time.sleep(move_time_ms / 1000.0 + WAIT_AFTER_MOVE_SECONDS)

    if ENABLE_MOTION and VERIFY_POSE_AFTER_MOVE:
        verify_pose(pose)


def move_gripper(
    angle: int,
    step_name: str,
    move_time_ms: int = GRIPPER_MOVE_TIME_MS,
) -> None:
    robot = require_robot()
    validate_angle(angle)
    update_state(current_step=step_name)

    logger.info(
        "GRIPPER step=%s angle=%s time_ms=%s enabled=%s",
        step_name,
        angle,
        move_time_ms,
        ENABLE_MOTION,
    )

    if ENABLE_MOTION:
        robot.Arm_serial_servo_write(6, angle, move_time_ms)

    time.sleep(move_time_ms / 1000.0 + WAIT_AFTER_GRIP_SECONDS)


def safe_home() -> None:
    logger.info("Đang thử đưa robot về HOME")
    try:
        move_pose("HOME", HOME_MOVE_TIME_MS)
    except Exception:
        logger.exception("Không thể đưa robot về HOME")
