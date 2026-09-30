from __future__ import annotations

import logging
import queue
import threading
import time
import traceback
import uuid
from contextlib import contextmanager
from typing import Any, Callable

import pick_guard

from robot_config import (
    ALLOW_UNCALIBRATED_DEFECT_CYCLES,
    CLASS_TO_BOX,
    GRIPPER_CLOSE,
    GRIPPER_OPEN,
    HOME_MOVE_TIME_MS,
    MAX_PICK_START_DELAY_MS,
    validate_angle,
)
from robot_motion import (
    move_gripper,
    move_pose,
    require_pose_reached,
    safe_home,
)
from robot_state import (
    get_job_copy,
    job_queue,
    jobs,
    motion_lock,
    now_iso,
    robot_state,
    set_job_fields,
    shutdown_event,
    state_lock,
    update_state,
)


logger = logging.getLogger("dofbot-api")
worker_thread: threading.Thread | None = None
vision_snapshot: Callable | None = None


@contextmanager
def admission_lock():
    if not motion_lock.acquire(blocking=False):
        raise RuntimeError("Robot motion already in progress")
    try:
        with state_lock:
            yield
    finally:
        motion_lock.release()


def execute_pick_sequence(
    job_id: str,
    class_name: str,
    wrist_angle: int | None = None,
    start_delay_ms: int = 0,
    prepositioned: bool = False,
    source: str = "api",
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

    motion_started = False
    try:
        if source == "arduino_checkpoint" and pick_guard.ENABLED:
            if vision_snapshot is None:
                raise RuntimeError("Camera guard is not initialized")
            set_job_fields(job_id, current_step="WAITING_FOR_STABLE_TARGET")
            update_state(current_step="WAITING_FOR_STABLE_TARGET")
            evidence = pick_guard.wait_for_target(vision_snapshot, shutdown_event)
            set_job_fields(job_id, pick_guard=evidence)
        if start_delay_ms > 0:
            logger.info(
                "Job %s chờ %s ms để đồng bộ băng tải",
                job_id,
                start_delay_ms,
            )
            time.sleep(start_delay_ms / 1000.0)

        update_state(state="busy", current_step="PREPARE_PICK")

        if not prepositioned:
            motion_started = True
            move_pose("HOME", HOME_MOVE_TIME_MS)
            move_gripper(GRIPPER_OPEN, "OPEN_GRIPPER")
            move_pose("PICK_ABOVE", wrist_angle=wrist_angle)
        else:
            checkpoint_confirmation = require_pose_reached("HOME")
            logger.info(
                "Checkpoint HOME confirmed as %s; skip duplicate "
                "HOME/OPEN_GRIPPER/PICK_ABOVE commands",
                checkpoint_confirmation,
            )

        motion_started = True
        move_pose("PICK_DOWN", wrist_angle=wrist_angle)
        move_gripper(GRIPPER_CLOSE, "CLOSE_GRIPPER")
        move_pose("PICK_LIFT", wrist_angle=wrist_angle)
        move_pose(box_config["above"])
        move_pose(box_config["drop"])
        move_gripper(GRIPPER_OPEN, "RELEASE_OBJECT")
        move_pose(
            box_config["above"],
            gripper_angle=GRIPPER_OPEN,
        )
        move_pose("HOME", HOME_MOVE_TIME_MS)
        home_confirmation = require_pose_reached("HOME")

        completed_at = now_iso()
        with state_lock:
            set_job_fields(
                job_id, status="completed", current_step=None,
                completed_at=completed_at, error=None,
                pose_confirmation=home_confirmation,
                conveyor_release_allowed=True,
            )
            update_state(
                state="vision_ready", busy=False, current_job_id=None,
                current_class=None, current_step=None,
                last_completed_job=job_id, last_error=None,
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
            conveyor_release_allowed=False,
        )
        update_state(
            state="error",
            busy=False,
            current_job_id=None,
            current_class=None,
            current_step=None,
            last_error=error_message,
        )
        if motion_started:
            safe_home()


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
        start_delay_ms = int(job.get("start_delay_ms", 0))
        prepositioned = bool(job.get("prepositioned", False))

        try:
            with motion_lock:
                execute_pick_sequence(
                    job_id,
                    class_name,
                    wrist_angle=wrist_angle,
                    start_delay_ms=start_delay_ms,
                    prepositioned=prepositioned,
                    source=str(job.get("source", "api")),
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


def stop_worker(timeout: float = 2.0) -> None:
    try:
        job_queue.put_nowait(None)
    except queue.Full:
        pass

    if (
        worker_thread is not None
        and worker_thread.is_alive()
        and threading.current_thread() is not worker_thread
    ):
        worker_thread.join(timeout=timeout)


def is_worker_alive() -> bool:
    return worker_thread is not None and worker_thread.is_alive()


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

    with admission_lock():
        if not robot_state.get("motion_enabled") or shutdown_event.is_set():
            raise RuntimeError("Robot motion is disabled or shutting down")
        if jobs.get(job_id) is not None:
            raise KeyError("job_id đã tồn tại; query existing job, do not repeat motion")
        if robot_state.get("busy") or not job_queue.empty():
            raise RuntimeError("Robot already has an active motion or reserved job")
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
            "conveyor_release_allowed": False,
        }
        jobs[job_id] = job
        job_queue.put({
            "job_id": job_id,
            "class_name": class_name,
            "wrist_angle": wrist_angle,
            "start_delay_ms": start_delay_ms,
            "prepositioned": prepositioned,
            "source": source,
        })
        # Reserve before the worker removes the queue item so a concurrent
        # request cannot slip between queue.get() and execute_pick_sequence().
        update_state(busy=True, current_job_id=job_id, current_class=class_name, state="queued")

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
