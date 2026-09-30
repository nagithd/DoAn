from __future__ import annotations

import queue
import threading
from copy import deepcopy
from datetime import datetime
from typing import Any

from robot_config import ENABLE_MOTION


# Chỉ một luồng được phép sử dụng bus điều khiển chuyển động tại một thời điểm.
motion_lock = threading.Lock()
state_lock = threading.RLock()

job_queue: queue.Queue[dict[str, Any] | None] = queue.Queue()
shutdown_event = threading.Event()

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


def now_iso() -> str:
    return datetime.now().astimezone().isoformat(timespec="seconds")


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


def get_all_jobs_copy() -> list[dict[str, Any]]:
    with state_lock:
        result = list(deepcopy(jobs).values())
    result.sort(key=lambda item: item["created_at"], reverse=True)
    return result
