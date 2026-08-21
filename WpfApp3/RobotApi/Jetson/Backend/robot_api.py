from __future__ import annotations

import atexit
import logging
import queue
import signal
import sys
import threading
import time
import traceback
import uuid
from copy import deepcopy
from datetime import datetime
from typing import Any

from flask import Flask, jsonify, request
from Arm_Lib import Arm_Device
from vision_trigger import (
    VisionTriggerController,
    register_vision_routes,
)


# ============================================================
# CẤU HÌNH CHUNG
# ============================================================

API_HOST = "0.0.0.0"
API_PORT = 7000

# Giữ False trong lúc kiểm tra API và hiệu chỉnh pose.
# Chỉ đổi thành True khi vùng hoạt động đã an toàn.
ENABLE_MOTION = True

DEFAULT_MOVE_TIME_MS = 1300
HOME_MOVE_TIME_MS = 1500
GRIPPER_MOVE_TIME_MS = 600
MAX_PICK_START_DELAY_MS = 30000

# The three defect drop poses must be confirmed on the real cell before
# automatic motion is allowed. Keep this False until every defect cycle has
# been calibrated and collision-tested.
ALLOW_UNCALIBRATED_DEFECT_CYCLES = False

WAIT_AFTER_MOVE_SECONDS = 0.65
WAIT_AFTER_GRIP_SECONDS = 0.25

SERVO_MIN_ANGLE = 0
SERVO_MAX_ANGLE = 180

# Sai số dùng khi kiểm tra góc servo.
SERVO_TOLERANCE_DEGREES = 6

# Servo số 6 là kẹp.
GRIPPER_OPEN = 40
GRIPPER_CLOSE = 145

# Đặt True để đọc lại servo sau mỗi pose.
# Việc đọc servo làm chu trình chậm hơn.
VERIFY_POSE_AFTER_MOVE = False

# False: critical HOME/checkpoint confirmation uses only the most recent pose
# command and never touches I2C automatically. /robot/servos remains available
# for an explicit diagnostic read. Set True only after feedback is stable.
VERIFY_REQUIRED_POSES_FROM_FEEDBACK = False

# Nếu một servo đọc được nhưng lệch pose, chương trình vẫn dừng chu trình.
# Nếu toàn bộ/ một phần feedback không đọc được, có thể dùng pose lệnh gần nhất
# làm fallback. Đây là trạng thái open-loop và được công khai qua /robot/status.
STRICT_SERVO_VERIFICATION = False
ALLOW_COMMANDED_POSE_FALLBACK = True

# Critical checkpoint/HOME verification is performed independently of
# VERIFY_POSE_AFTER_MOVE. A readable mismatch is always an error. Missing
# feedback may fall back to the most recent successfully submitted pose command.
REQUIRED_POSE_VERIFY_ATTEMPTS = 3
REQUIRED_POSE_VERIFY_DELAY_SECONDS = 0.20


# ============================================================
# CẤU HÌNH POSE
# Thứ tự:
# [servo1, servo2, servo3, servo4, servo5, servo6]
#
# Các giá trị hiện tại chỉ là giá trị khởi đầu.
# Cần hiệu chỉnh theo vị trí thật của băng tải và các hộp.
# ============================================================

POSES: dict[str, list[int]] = {
    "HOME": [
        180, 130, 0, 0, 90, GRIPPER_OPEN
    ],

    # Điểm gắp cố định
    "PICK_ABOVE": [
        180, 130, 0, 0, 90, GRIPPER_OPEN
    ],
    "PICK_DOWN": [
        180, 83, 25, 25, 90, GRIPPER_OPEN
    ],
    "PICK_LIFT": [
        180, 85, 45, 50, 90, GRIPPER_CLOSE
    ],

    "DROP_DENTED_ABOVE": [
        93, 85, 45, 50, 90, GRIPPER_CLOSE
    ],
    "DROP_DENTED": [
        93, 70, 30, 35, 90, GRIPPER_CLOSE
    ],

    "DROP_SCRATCHED_ABOVE": [
        70, 85, 45, 50, 90, GRIPPER_CLOSE
    ],
    "DROP_SCRATCHED": [
        70, 70, 30, 35, 90, GRIPPER_CLOSE
    ],

    "DROP_SWOLLEN_ABOVE": [
        115, 85, 45, 50, 90, GRIPPER_CLOSE
    ],
    "DROP_SWOLLEN": [
        115, 70, 30, 35, 90, GRIPPER_CLOSE
    ],
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


# ============================================================
# LOGGING
# ============================================================

logging.basicConfig(
    level=logging.INFO,
    format=(
        "%(asctime)s | %(levelname)s | "
        "%(threadName)s | %(message)s"
    ),
)

logger = logging.getLogger("dofbot-api")


# ============================================================
# FLASK VÀ BIẾN HỆ THỐNG
# ============================================================

app = Flask(__name__)

arm: Arm_Device | None = None

# Chỉ worker được phép thực hiện chuyển động robot.
motion_lock = threading.Lock()

# Bảo vệ robot_state và jobs.
state_lock = threading.RLock()

job_queue: queue.Queue[dict[str, Any] | None] = queue.Queue()

shutdown_event = threading.Event()

worker_thread: threading.Thread | None = None

robot_state: dict[str, Any] = {
    "state": "initializing",
    "busy": False,
    "motion_enabled": ENABLE_MOTION,
    "current_job_id": None,
    "current_class": None,
    "current_step": None,
    "last_error": None,
    "last_completed_job": None,
    "last_routing_event": None,
    "commanded_pose_name": None,
    "commanded_pose": None,
    "pose_confirmation": "unknown",
    "servo_feedback_available": None,
    "last_servo_feedback": None,
    "last_servo_feedback_at": None,
    "queue_size": 0,
    "updated_at": None,
}

jobs: dict[str, dict[str, Any]] = {}


# ============================================================
# HÀM TIỆN ÍCH
# ============================================================

def now_iso() -> str:
    return datetime.now().astimezone().isoformat(
        timespec="seconds"
    )


def update_state(**changes: Any) -> None:
    with state_lock:
        robot_state.update(changes)
        robot_state["queue_size"] = job_queue.qsize()
        robot_state["updated_at"] = now_iso()


def get_state_copy() -> dict[str, Any]:
    with state_lock:
        result = deepcopy(robot_state)
        result["queue_size"] = job_queue.qsize()
        return result


def set_job_fields(job_id: str, **changes: Any) -> None:
    with state_lock:
        if job_id not in jobs:
            raise KeyError(f"Không tồn tại job: {job_id}")

        jobs[job_id].update(changes)


def get_job_copy(job_id: str) -> dict[str, Any] | None:
    with state_lock:
        job = jobs.get(job_id)
        return deepcopy(job) if job is not None else None


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
            f"Pose {name} phải có đúng 6 góc, "
            f"hiện có {len(pose)}"
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


# ============================================================
# ĐỌC VÀ KIỂM TRA SERVO
# ============================================================

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
            logger.exception(
                "Không đọc được servo %s",
                servo_id,
            )
            value = {
                "error": str(exc)
            }

        values.append(value)

    readable_count = sum(
        isinstance(value, (int, float))
        for value in values
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
        logger.warning(
            "Pose verification %s: %s",
            result,
            message,
        )

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

            # Repeating six reads cannot prove a pose when there is no matching
            # command history. Fail immediately instead of blocking the API.
            break

        if attempt < attempts:
            time.sleep(REQUIRED_POSE_VERIFY_DELAY_SECONDS)

    detail = "; ".join(last_errors) or last_result
    update_state(pose_confirmation=last_result)
    raise RuntimeError(
        f"Robot pose {pose_name} confirmation failed: "
        f"result={last_result}; {detail}"
    )


# ============================================================
# HÀM ĐIỀU KHIỂN ROBOT
# ============================================================

def move_pose(
    pose_name: str,
    move_time_ms: int = DEFAULT_MOVE_TIME_MS,
    wrist_angle: int | None = None,
) -> None:
    robot = require_robot()

    if pose_name not in POSES:
        raise KeyError(f"Không tồn tại pose: {pose_name}")

    pose = list(POSES[pose_name])

    if wrist_angle is not None:
        validate_angle(wrist_angle)
        pose[4] = int(round(wrist_angle))

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
        robot.Arm_serial_servo_write6_array(
            pose,
            move_time_ms,
        )
        update_state(
            commanded_pose_name=pose_name,
            commanded_pose=deepcopy(pose),
            pose_confirmation="commanded",
        )

    time.sleep(
        move_time_ms / 1000.0
        + WAIT_AFTER_MOVE_SECONDS
    )

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
        robot.Arm_serial_servo_write(
            6,
            angle,
            move_time_ms,
        )

    time.sleep(
        move_time_ms / 1000.0
        + WAIT_AFTER_GRIP_SECONDS
    )


def safe_home() -> None:
    logger.info("Đang thử đưa robot về HOME")

    try:
        move_pose(
            "HOME",
            HOME_MOVE_TIME_MS,
        )
    except Exception:
        logger.exception("Không thể đưa robot về HOME")


def execute_pick_sequence(
    job_id: str,
    class_name: str,
    wrist_angle: int | None = None,
    start_delay_ms: int = 0,
    prepositioned: bool = False,
) -> None:
    box_config = CLASS_TO_BOX[class_name]

    set_job_fields(
        job_id,
        status="running",
        started_at=now_iso(),
        current_step=(
            "WAITING_FOR_TARGET"
            if start_delay_ms > 0
            else "PREPARE_PICK"
        ),
    )

    update_state(
        state=(
            "waiting_for_target"
            if start_delay_ms > 0
            else "busy"
        ),
        busy=True,
        current_job_id=job_id,
        current_class=class_name,
        current_step=(
            "WAITING_FOR_TARGET"
            if start_delay_ms > 0
            else "PREPARE_PICK"
        ),
        last_error=None,
    )

    try:
        if start_delay_ms > 0:
            logger.info(
                "Job %s chờ %s ms để đồng bộ băng tải",
                job_id,
                start_delay_ms,
            )
            time.sleep(start_delay_ms / 1000.0)

        update_state(
            state="busy",
            current_step="PREPARE_PICK",
        )

        # A prepositioned checkpoint job is accepted only when the API state is
        # vision_ready. HOME already equals PICK_ABOVE and the gripper is open,
        # so repeating those commands would add several seconds with no visible
        # motion. Manual/non-prepositioned jobs still prepare from HOME.
        if not prepositioned:
            move_pose("HOME", HOME_MOVE_TIME_MS)
            move_gripper(
                GRIPPER_OPEN,
                "OPEN_GRIPPER",
            )
            move_pose(
                "PICK_ABOVE",
                wrist_angle=wrist_angle,
            )
        else:
            # Software state alone is insufficient: a previous dropped servo
            # command can leave the arm away from HOME while state=vision_ready.
            checkpoint_confirmation = require_pose_reached("HOME")
            logger.info(
                "Checkpoint HOME confirmed as %s; skip duplicate "
                "HOME/OPEN_GRIPPER/PICK_ABOVE commands",
                checkpoint_confirmation,
            )

        # 4. Hạ xuống vị trí gắp
        move_pose(
            "PICK_DOWN",
            wrist_angle=wrist_angle,
        )

        # 5. Đóng kẹp
        move_gripper(
            GRIPPER_CLOSE,
            "CLOSE_GRIPPER",
        )

        # 6. Nâng vật lên
        move_pose(
            "PICK_LIFT",
            wrist_angle=wrist_angle,
        )

        # 7. Di chuyển đến phía trên hộp
        move_pose(box_config["above"])

        # 8. Hạ xuống vị trí thả trong khi vẫn giữ chặt pin. Servo 6 của
        # các pose DROP_* phải là GRIPPER_CLOSE; việc mở kẹp được thực hiện
        # riêng ở bước RELEASE_OBJECT sau khi chuyển động hạ đã hoàn tất.
        move_pose(box_config["drop"])

        # 9. Thả vật
        move_gripper(
            GRIPPER_OPEN,
            "RELEASE_OBJECT",
        )

        # 10. Nâng khỏi hộp
        move_pose(box_config["above"])

        # 11. HOME is also the camera waiting pose. Every completed pick
        # returns here, regardless of whether it came from vision or the API.
        move_pose("HOME", HOME_MOVE_TIME_MS)
        home_confirmation = require_pose_reached("HOME")

        completed_at = now_iso()

        set_job_fields(
            job_id,
            status="completed",
            current_step=None,
            completed_at=completed_at,
            error=None,
            pose_confirmation=home_confirmation,
        )

        update_state(
            state="vision_ready",
            busy=False,
            current_job_id=None,
            current_class=None,
            current_step=None,
            last_completed_job=job_id,
            last_error=None,
        )

        logger.info(
            "Job hoàn tất: id=%s class=%s home_confirmation=%s",
            job_id,
            class_name,
            home_confirmation,
        )

    except Exception as exc:
        error_message = str(exc)

        logger.exception(
            "Job thất bại: id=%s class=%s",
            job_id,
            class_name,
        )

        set_job_fields(
            job_id,
            status="error",
            current_step=None,
            completed_at=now_iso(),
            error=error_message,
        )

        update_state(
            state="error",
            busy=False,
            current_job_id=None,
            current_class=None,
            current_step=None,
            last_error=error_message,
        )

        safe_home()


# ============================================================
# WORKER XỬ LÝ HÀNG ĐỢI
# ============================================================

def robot_worker() -> None:
    logger.info("Robot worker đã khởi động")

    while not shutdown_event.is_set():
        try:
            job = job_queue.get(timeout=0.5)
        except queue.Empty:
            continue

        if job is None:
            job_queue.task_done()
            break

        job_id = str(job["job_id"])
        class_name = str(job["class_name"])
        wrist_angle = job.get("wrist_angle")
        start_delay_ms = int(
            job.get("start_delay_ms", 0)
        )
        prepositioned = bool(
            job.get("prepositioned", False)
        )

        try:
            with motion_lock:
                execute_pick_sequence(
                    job_id,
                    class_name,
                    wrist_angle=wrist_angle,
                    start_delay_ms=start_delay_ms,
                    prepositioned=prepositioned,
                )
        except Exception:
            # execute_pick_sequence đã ghi trạng thái lỗi.
            logger.error(
                "Worker gặp lỗi ngoài dự kiến:\n%s",
                traceback.format_exc(),
            )
        finally:
            job_queue.task_done()
            update_state()

    logger.info("Robot worker đã dừng")


def start_worker() -> None:
    global worker_thread

    if worker_thread is not None and worker_thread.is_alive():
        return

    worker_thread = threading.Thread(
        target=robot_worker,
        name="robot-worker",
        daemon=True,
    )

    worker_thread.start()


# ============================================================
# KHỞI TẠO VÀ TẮT HỆ THỐNG
# ============================================================

def initialize_robot() -> None:
    global arm

    logger.info("Đang kiểm tra cấu hình pose")
    validate_all_configuration()

    logger.info("Đang khởi tạo Arm_Device")
    arm = Arm_Device()

    update_state(
        state="idle",
        busy=False,
        current_job_id=None,
        current_class=None,
        current_step=None,
        last_error=None,
    )

    start_worker()

    logger.info("Robot API đã khởi tạo thành công")


def shutdown_system() -> None:
    if shutdown_event.is_set():
        return

    logger.info("Đang dừng Robot API")
    shutdown_event.set()

    try:
        job_queue.put_nowait(None)
    except queue.Full:
        pass

    controller = globals().get("vision_controller")
    if controller is not None:
        try:
            controller.stop()
        except Exception:
            logger.exception("Không thể dừng vision trigger")

    if (
        worker_thread is not None
        and worker_thread.is_alive()
        and threading.current_thread() is not worker_thread
    ):
        worker_thread.join(timeout=2.0)


def signal_handler(
    signum: int,
    _frame: Any,
) -> None:
    logger.info("Nhận signal %s", signum)
    shutdown_system()
    sys.exit(0)


atexit.register(shutdown_system)

signal.signal(signal.SIGINT, signal_handler)
signal.signal(signal.SIGTERM, signal_handler)


# ============================================================
# HTTP API
# ============================================================

@app.get("/health")
def health():
    state = get_state_copy()
    return jsonify({
        "status": "ok",
        "service": "dofbot-robot-api",
        "motion_enabled": ENABLE_MOTION,
        "robot_initialized": arm is not None,
        "worker_alive": (
            worker_thread is not None
            and worker_thread.is_alive()
        ),
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
        "vision_available": "vision_controller" in globals(),
        "inspection_classes": sorted(INSPECTION_CLASS_ACTIONS.keys()),
        "time": now_iso(),
    })


@app.get("/robot/status")
def robot_status():
    return jsonify({
        "success": True,
        "robot": get_state_copy(),
    })


@app.get("/robot/poses")
def robot_poses():
    return jsonify({
        "success": True,
        "poses": deepcopy(POSES),
        "class_to_box": deepcopy(CLASS_TO_BOX),
        "inspection_class_actions": deepcopy(INSPECTION_CLASS_ACTIONS),
        "uncalibrated_cycles_allowed": ALLOW_UNCALIBRATED_DEFECT_CYCLES,
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
        "inspection_class_actions": deepcopy(INSPECTION_CLASS_ACTIONS),
        "defect_cycles": deepcopy(CLASS_TO_BOX),
        "last_routing_event": get_state_copy().get("last_routing_event"),
    })


@app.get("/robot/servos")
def robot_servos():
    state = get_state_copy()
    if state["busy"] or not motion_lock.acquire(blocking=False):
        return jsonify({
            "success": False,
            "error": "Servo feedback is unavailable while robot is moving",
            "robot": state,
        }), 409

    try:
        values = read_servos()
        return jsonify({
            "success": True,
            "servos": values,
            "feedback_available": all(
                isinstance(value, (int, float))
                for value in values
            ),
            "time": now_iso(),
        })

    except Exception as exc:
        logger.exception("Không thể đọc servo")

        return jsonify({
            "success": False,
            "error": str(exc),
        }), 500

    finally:
        motion_lock.release()


def parse_manual_move_time(
    data: dict[str, Any],
    default_value: int,
) -> int:
    move_time_ms = int(
        data.get("move_time_ms", default_value)
    )

    if not 100 <= move_time_ms <= 5000:
        raise ValueError(
            "move_time_ms phải nằm trong khoảng 100–5000"
        )

    return move_time_ms


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

    if not motion_lock.acquire(blocking=False):
        return jsonify({
            "success": False,
            "error": "Robot đang bận",
            "robot": get_state_copy(),
        }), 409

    try:
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

        return jsonify({
            "success": False,
            "error": str(exc),
        }), 500

    finally:
        motion_lock.release()


def enqueue_pick_job(
    class_name: str,
    requested_job_id: str = "",
    wrist_angle: int | None = None,
    start_delay_ms: int = 0,
    prepositioned: bool = False,
    source: str = "api",
) -> dict[str, Any]:
    if class_name not in CLASS_TO_BOX:
        raise ValueError(
            "class_name không hợp lệ; cho phép: "
            + ", ".join(sorted(CLASS_TO_BOX.keys()))
        )

    cycle_config = CLASS_TO_BOX[class_name]
    if (
        not bool(cycle_config.get("calibrated", False))
        and not ALLOW_UNCALIBRATED_DEFECT_CYCLES
    ):
        raise ValueError(
            f"Cycle {class_name} is not calibrated. "
            "Confirm the above/drop poses and set calibrated=true first."
        )

    if wrist_angle is not None:
        validate_angle(wrist_angle)
        wrist_angle = int(round(wrist_angle))

    if not 0 <= start_delay_ms <= MAX_PICK_START_DELAY_MS:
        raise ValueError(
            "start_delay_ms phải nằm trong khoảng "
            f"0–{MAX_PICK_START_DELAY_MS}"
        )

    job_id = requested_job_id or str(uuid.uuid4())

    with state_lock:
        if prepositioned:
            current_state = str(robot_state.get("state", "unknown"))
            current_busy = bool(robot_state.get("busy", False))
            queued_jobs = job_queue.qsize()

            if (
                current_state != "vision_ready"
                or current_busy
                or queued_jobs > 0
            ):
                raise RuntimeError(
                    "Prepositioned checkpoint job requires "
                    "state=vision_ready, busy=false and an empty queue; "
                    f"state={current_state}, busy={current_busy}, "
                    f"queue_size={queued_jobs}"
                )

        existing_job = jobs.get(job_id)

        if existing_job is not None:
            raise KeyError("job_id đã tồn tại")

        job = {
            "job_id": job_id,
            "class_name": class_name,
            "wrist_angle": wrist_angle,
            "start_delay_ms": start_delay_ms,
            "prepositioned": prepositioned,
            "source": source,
            "status": "queued",
            "created_at": now_iso(),
            "started_at": None,
            "completed_at": None,
            "current_step": None,
            "error": None,
        }

        jobs[job_id] = job
        # Put the job into the queue while state_lock is still held. A second
        # prepositioned request will then observe queue_size > 0 and be rejected
        # instead of slipping through between validation and queue insertion.
        job_queue.put({
            "job_id": job_id,
            "class_name": class_name,
            "wrist_angle": wrist_angle,
            "start_delay_ms": start_delay_ms,
            "prepositioned": prepositioned,
        })

    update_state()

    logger.info(
        "Đã nhận job: id=%s class=%s wrist=%s delay_ms=%s source=%s",
        job_id,
        class_name,
        wrist_angle,
        start_delay_ms,
        source,
    )

    return get_job_copy(job_id) or job


@app.post("/robot/pick")
def robot_pick():
    data = request.get_json(silent=True) or {}

    class_name = str(
        data.get("class_name", "")
    ).strip().lower()

    requested_job_id = str(
        data.get("job_id", "")
    ).strip()

    wrist_value = data.get("wrist_angle")

    try:
        wrist_angle = (
            None
            if wrist_value is None
            else int(round(float(wrist_value)))
        )
        start_delay_ms = int(
            data.get("start_delay_ms", 0)
        )
        prepositioned_value = data.get(
            "prepositioned",
            False,
        )

        if not isinstance(prepositioned_value, bool):
            raise ValueError(
                "prepositioned phải là true hoặc false"
            )

        job = enqueue_pick_job(
            class_name=class_name,
            requested_job_id=requested_job_id,
            wrist_angle=wrist_angle,
            start_delay_ms=start_delay_ms,
            prepositioned=prepositioned_value,
            source="api",
        )
    except KeyError as exc:
        return jsonify({
            "success": False,
            "error": str(exc),
        }), 409
    except RuntimeError as exc:
        return jsonify({
            "success": False,
            "error": str(exc),
        }), 409
    except (TypeError, ValueError) as exc:
        return jsonify({
            "success": False,
            "error": str(exc),
            "allowed_classes": sorted(
                CLASS_TO_BOX.keys()
            ),
        }), 400

    return jsonify({
        "success": True,
        "accepted": True,
        "job": job,
        "queue_size": job_queue.qsize(),
    }), 202


def handle_vision_trigger(
    trigger: dict[str, Any],
) -> dict[str, Any]:
    class_name = str(
        trigger["class_name"]
    ).strip().lower()

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
        start_delay_ms=int(
            trigger["start_delay_ms"]
        ),
        prepositioned=True,
        source="vision",
    )


vision_controller = VisionTriggerController(
    on_trigger=handle_vision_trigger,
    get_robot_state=get_state_copy,
)
register_vision_routes(
    app,
    vision_controller,
)


@app.get("/robot/jobs/<job_id>")
def robot_job(job_id: str):
    job = get_job_copy(job_id)

    if job is None:
        return jsonify({
            "success": False,
            "error": "Không tìm thấy job",
        }), 404

    return jsonify({
        "success": True,
        "job": job,
    })


@app.get("/robot/jobs")
def robot_jobs():
    with state_lock:
        all_jobs = list(
            deepcopy(jobs).values()
        )

    all_jobs.sort(
        key=lambda item: item["created_at"],
        reverse=True,
    )

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
        update_state(
            state="homing",
            busy=True,
            current_step="HOME",
            last_error=None,
        )

        move_pose(
            "HOME",
            HOME_MOVE_TIME_MS,
        )
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

        return jsonify({
            "success": False,
            "error": str(exc),
        }), 500

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
        update_state(
            state="resetting",
            busy=True,
            current_step="HOME",
            last_error=None,
        )

        move_pose(
            "HOME",
            HOME_MOVE_TIME_MS,
        )
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
        return jsonify({
            "success": False,
            "error": str(exc),
        }), 500

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


# ============================================================
# MAIN
# ============================================================

def main() -> None:
    try:
        initialize_robot()

        logger.info("=" * 60)
        logger.info("DOFBOT ROBOT API")
        logger.info("Motion enabled: %s", ENABLE_MOTION)
        logger.info(
            "Listening on http://%s:%s",
            API_HOST,
            API_PORT,
        )
        logger.info("=" * 60)

        app.run(
            host=API_HOST,
            port=API_PORT,
            debug=False,
            threaded=True,
            use_reloader=False,
        )

    except Exception:
        logger.critical(
            "Không thể khởi động Robot API:\n%s",
            traceback.format_exc(),
        )
        raise


if __name__ == "__main__":
    main()
