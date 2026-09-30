from __future__ import annotations

from typing import Any


API_HOST = "0.0.0.0"
API_PORT = 7000

# Chỉ đổi thành False khi cần kiểm tra API mà không cho robot chuyển động.
ENABLE_MOTION = True

DEFAULT_MOVE_TIME_MS = 1300
HOME_MOVE_TIME_MS = 1500
GRIPPER_MOVE_TIME_MS = 600
MAX_PICK_START_DELAY_MS = 30000

ALLOW_UNCALIBRATED_DEFECT_CYCLES = False

WAIT_AFTER_MOVE_SECONDS = 0.65
WAIT_AFTER_GRIP_SECONDS = 0.25

SERVO_MIN_ANGLE = 0
SERVO_MAX_ANGLE = 180
SERVO_TOLERANCE_DEGREES = 6
GRIPPER_OPEN = 40
GRIPPER_CLOSE = 147

VERIFY_POSE_AFTER_MOVE = False
VERIFY_REQUIRED_POSES_FROM_FEEDBACK = False
STRICT_SERVO_VERIFICATION = False
ALLOW_COMMANDED_POSE_FALLBACK = True
REQUIRED_POSE_VERIFY_ATTEMPTS = 3
REQUIRED_POSE_VERIFY_DELAY_SECONDS = 0.20


# Thứ tự: [servo1, servo2, servo3, servo4, servo5, servo6]
POSES: dict[str, list[int]] = {
    "HOME": [180, 130, 0, 0, 90, GRIPPER_OPEN],
    "PICK_ABOVE": [180, 130, 0, 0, 90, GRIPPER_OPEN],
    "PICK_DOWN": [180, 83, 25, 25, 90, GRIPPER_OPEN],
    "PICK_LIFT": [180, 85, 45, 50, 90, GRIPPER_CLOSE],
    "DROP_DENTED_ABOVE": [93, 85, 45, 50, 90, GRIPPER_CLOSE],
    "DROP_DENTED": [93, 70, 30, 35, 90, GRIPPER_CLOSE],
    "DROP_SCRATCHED_ABOVE": [70, 85, 45, 50, 90, GRIPPER_CLOSE],
    "DROP_SCRATCHED": [70, 70, 30, 35, 90, GRIPPER_CLOSE],
    "DROP_SWOLLEN_ABOVE": [115, 85, 45, 50, 90, GRIPPER_CLOSE],
    "DROP_SWOLLEN": [115, 70, 30, 35, 90, GRIPPER_CLOSE],
}


CLASS_TO_BOX: dict[str, dict[str, Any]] = {
    "dented": {
        "above": "DROP_DENTED_ABOVE",
        "drop": "DROP_DENTED",
        "calibrated": True,
    },
    "scratched": {
        "above": "DROP_SCRATCHED_ABOVE",
        "drop": "DROP_SCRATCHED",
        "calibrated": True,
    },
    "swollen": {
        "above": "DROP_SWOLLEN_ABOVE",
        "drop": "DROP_SWOLLEN",
        "calibrated": True,
    },
}


INSPECTION_CLASS_ACTIONS: dict[str, dict[str, str]] = {
    "dented": {"action": "robot_pick", "cycle": "DENTED"},
    "normal": {"action": "pass", "cycle": "PASS"},
    "scratched": {"action": "robot_pick", "cycle": "SCRATCHED"},
    "swollen": {"action": "robot_pick", "cycle": "SWOLLEN"},
}


def validate_angle(angle: int | float) -> None:
    if not isinstance(angle, (int, float)):
        raise TypeError(f"Góc servo không phải số: {angle!r}")

    if not SERVO_MIN_ANGLE <= angle <= SERVO_MAX_ANGLE:
        raise ValueError(
            f"Góc servo {angle} nằm ngoài "
            f"{SERVO_MIN_ANGLE}–{SERVO_MAX_ANGLE}"
        )


def validate_pose(name: str, pose: list[int]) -> None:
    if not isinstance(pose, list):
        raise TypeError(f"Pose {name} phải là một list")

    if len(pose) != 6:
        raise ValueError(
            f"Pose {name} phải có đúng 6 góc, hiện có {len(pose)}"
        )

    for servo_id, angle in enumerate(pose, start=1):
        try:
            validate_angle(angle)
        except Exception as exc:
            raise ValueError(
                f"Pose {name}, servo {servo_id}: {exc}"
            ) from exc


def validate_all_configuration() -> None:
    for pose_name, pose in POSES.items():
        validate_pose(pose_name, pose)

    for class_name, box_config in CLASS_TO_BOX.items():
        above = box_config.get("above")
        drop = box_config.get("drop")

        if above not in POSES:
            raise ValueError(
                f"Class {class_name}: không tồn tại pose {above}"
            )
        if drop not in POSES:
            raise ValueError(
                f"Class {class_name}: không tồn tại pose {drop}"
            )
        if not isinstance(box_config.get("calibrated"), bool):
            raise ValueError(
                f"Class {class_name}: calibrated must be true or false"
            )
