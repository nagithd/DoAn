from __future__ import annotations

import json
import logging
import threading
import time
import uuid
from collections import deque
from dataclasses import asdict, dataclass, fields
from pathlib import Path
from typing import Any, Callable

from flask import Flask, Response, jsonify, request

try:
    import cv2
    import numpy as np
except ImportError:
    cv2 = None
    np = None


logger = logging.getLogger("dofbot-vision")

INSPECTION_CLASS_ACTIONS = {
    "dented": "robot_pick",
    "normal": "pass",
    "scratched": "robot_pick",
    "swollen": "robot_pick",
}


@dataclass
class VisionTriggerConfig:
    camera_index: int = 0
    frame_width: int = 640
    frame_height: int = 480

    # One centered region is both the processing ROI and trigger zone.
    # Ratios keep the zone stable when the camera resolution changes.
    entry_zone_center_x_ratio: float = 0.50
    entry_zone_center_y_ratio: float = 0.50
    entry_zone_width_ratio: float = 0.70
    entry_zone_height_ratio: float = 0.45

    min_contour_area: float = 1200.0
    max_contour_area_ratio: float = 0.40
    stable_frames: int = 4
    clear_frames_to_rearm: int = 12
    warmup_frames: int = 30

    background_history: int = 200
    background_threshold: float = 25.0

    # Image angle of the conveyor direction: 0 = left/right, 90 = vertical.
    conveyor_angle_deg: float = 90.0
    wrist_reference_angle: float = 90.0
    wrist_direction: int = 1
    wrist_min_angle: float = 20.0
    wrist_max_angle: float = 160.0
    max_wrist_correction_deg: float = 45.0
    use_wrist_correction: bool = True

    # Timing model for a continuously moving conveyor.
    belt_speed_mm_s: float = 40.0
    distance_to_pick_mm: float = 200.0
    robot_time_to_grip_ms: int = 4150
    processing_margin_ms: int = 350
    reject_late_trigger: bool = True

    # Kept only so older vision_config.json files remain loadable. Runtime
    # routing uses the FIFO AI classification queue instead.
    default_class: str = "normal"
    min_ai_confidence: float = 0.40
    classification_ttl_seconds: float = 30.0

    # Safety default: camera monitoring is allowed, robot motion is not.
    auto_trigger: bool = False


class VisionTriggerController:
    def __init__(
        self,
        on_trigger: Callable[[dict[str, Any]], dict[str, Any]],
        get_robot_state: Callable[[], dict[str, Any]],
        config_path: str = "/root/vision_config.json",
    ) -> None:
        self._on_trigger = on_trigger
        self._get_robot_state = get_robot_state
        self._config_path = Path(config_path)
        self._config = self._load_config()

        self._lock = threading.RLock()
        self._stop_event = threading.Event()
        self._thread: threading.Thread | None = None

        self._running = False
        self._camera_open = False
        self._frame_count = 0
        self._stable_count = 0
        self._clear_count = 0
        self._trigger_latched = False
        self._last_detection: dict[str, Any] | None = None
        self._last_trigger: dict[str, Any] | None = None
        self._last_job: dict[str, Any] | None = None
        self._last_error: str | None = None
        self._latest_jpeg: bytes | None = None
        self._previous_center: tuple[float, float] | None = None
        self._classification_queue: deque[dict[str, Any]] = deque()

    def _load_config(self) -> VisionTriggerConfig:
        config = VisionTriggerConfig()

        if not self._config_path.exists():
            return config

        try:
            data = json.loads(
                self._config_path.read_text(encoding="utf-8")
            )
            self._apply_config_values(config, data)
            self._validate_config(config)
        except Exception:
            logger.exception(
                "Cannot load vision config: %s",
                self._config_path,
            )

        return config

    @staticmethod
    def _apply_config_values(
        config: VisionTriggerConfig,
        data: dict[str, Any],
    ) -> None:
        allowed = {field.name for field in fields(config)}

        for key, value in data.items():
            if key in allowed:
                setattr(config, key, value)

    @staticmethod
    def _validate_config(config: VisionTriggerConfig) -> None:
        if config.camera_index < 0:
            raise ValueError("camera_index must be >= 0")

        if config.frame_width < 160 or config.frame_height < 120:
            raise ValueError("frame size is too small")

        if not 0 <= config.entry_zone_center_x_ratio <= 1:
            raise ValueError(
                "entry_zone_center_x_ratio must be between 0 and 1"
            )

        if not 0 <= config.entry_zone_center_y_ratio <= 1:
            raise ValueError(
                "entry_zone_center_y_ratio must be between 0 and 1"
            )

        if not 0.05 <= config.entry_zone_width_ratio <= 1:
            raise ValueError(
                "entry_zone_width_ratio must be between 0.05 and 1"
            )

        if not 0.05 <= config.entry_zone_height_ratio <= 1:
            raise ValueError(
                "entry_zone_height_ratio must be between 0.05 and 1"
            )

        half_width = config.entry_zone_width_ratio / 2.0
        half_height = config.entry_zone_height_ratio / 2.0
        if not (
            half_width
            <= config.entry_zone_center_x_ratio
            <= 1.0 - half_width
        ):
            raise ValueError(
                "entry zone width extends outside the camera frame"
            )

        if not (
            half_height
            <= config.entry_zone_center_y_ratio
            <= 1.0 - half_height
        ):
            raise ValueError(
                "entry zone height extends outside the camera frame"
            )

        if config.min_contour_area <= 0:
            raise ValueError("min_contour_area must be > 0")

        if not 0.01 <= config.max_contour_area_ratio <= 0.95:
            raise ValueError(
                "max_contour_area_ratio must be between 0.01 and 0.95"
            )

        if config.stable_frames < 1:
            raise ValueError("stable_frames must be >= 1")

        if config.clear_frames_to_rearm < 1:
            raise ValueError("clear_frames_to_rearm must be >= 1")

        if config.warmup_frames < 0:
            raise ValueError("warmup_frames must be >= 0")

        if config.wrist_direction not in {-1, 1}:
            raise ValueError("wrist_direction must be -1 or 1")

        if not (
            0
            <= config.wrist_min_angle
            < config.wrist_max_angle
            <= 180
        ):
            raise ValueError(
                "wrist angle limits must be within 0..180"
            )

        if config.max_wrist_correction_deg < 0:
            raise ValueError(
                "max_wrist_correction_deg must be >= 0"
            )

        if config.belt_speed_mm_s < 0:
            raise ValueError("belt_speed_mm_s must be >= 0")

        if config.distance_to_pick_mm < 0:
            raise ValueError("distance_to_pick_mm must be >= 0")

        if config.robot_time_to_grip_ms < 0:
            raise ValueError("robot_time_to_grip_ms must be >= 0")

        if config.processing_margin_ms < 0:
            raise ValueError("processing_margin_ms must be >= 0")

        if not config.default_class.strip():
            raise ValueError("default_class cannot be empty")

        if not 0 <= config.min_ai_confidence <= 1:
            raise ValueError("min_ai_confidence must be between 0 and 1")

        if config.classification_ttl_seconds <= 0:
            raise ValueError("classification_ttl_seconds must be > 0")

    def _save_config(self) -> None:
        self._config_path.parent.mkdir(
            parents=True,
            exist_ok=True,
        )
        self._config_path.write_text(
            json.dumps(
                asdict(self._config),
                ensure_ascii=False,
                indent=2,
            ),
            encoding="utf-8",
        )

    def update_config(
        self,
        values: dict[str, Any],
    ) -> dict[str, Any]:
        with self._lock:
            updated = VisionTriggerConfig(**asdict(self._config))
            self._apply_config_values(updated, values)
            self._validate_config(updated)
            self._config = updated
            self._save_config()
            return asdict(self._config)

    def get_config(self) -> dict[str, Any]:
        with self._lock:
            return asdict(self._config)

    def start(self) -> None:
        if cv2 is None or np is None:
            raise RuntimeError(
                "OpenCV Python is not installed in the container"
            )

        with self._lock:
            if self._running:
                return

            self._running = True
            self._last_error = None
            self._frame_count = 0
            self._stable_count = 0
            self._clear_count = 0
            self._trigger_latched = False
            self._previous_center = None
            self._stop_event.clear()
            self._thread = threading.Thread(
                target=self._capture_loop,
                name="vision-trigger",
                daemon=True,
            )
            self._thread.start()

    def stop(self) -> None:
        with self._lock:
            self._running = False
            self._stop_event.set()
            thread = self._thread

        if (
            thread is not None
            and thread.is_alive()
            and thread is not threading.current_thread()
        ):
            thread.join(timeout=3.0)

        with self._lock:
            self._thread = None
            self._camera_open = False

    def reset_trigger(self) -> None:
        with self._lock:
            self._trigger_latched = False
            self._stable_count = 0
            self._clear_count = 0
            self._previous_center = None
            self._last_error = None

    def submit_classification(
        self,
        values: dict[str, Any],
    ) -> dict[str, Any]:
        class_name = str(values.get("class_name", "")).strip().lower()
        if class_name not in INSPECTION_CLASS_ACTIONS:
            raise ValueError(
                "class_name must be one of: "
                + ", ".join(sorted(INSPECTION_CLASS_ACTIONS))
            )

        try:
            confidence = float(values.get("confidence"))
        except (TypeError, ValueError) as exc:
            raise ValueError("confidence must be a number between 0 and 1") from exc

        with self._lock:
            minimum = self._config.min_ai_confidence
            if not minimum <= confidence <= 1:
                raise ValueError(
                    f"confidence must be between {minimum:.2f} and 1"
                )

            inspection_id = str(
                values.get("inspection_id") or uuid.uuid4()
            ).strip()
            if any(
                item["inspection_id"] == inspection_id
                for item in self._classification_queue
            ):
                raise ValueError("inspection_id already exists in the queue")

            now = time.time()
            item = {
                "inspection_id": inspection_id,
                "class_name": class_name,
                "confidence": round(confidence, 6),
                "action": INSPECTION_CLASS_ACTIONS[class_name],
                "received_at": now,
                "expires_at": now + self._config.classification_ttl_seconds,
                "source": str(values.get("source") or "windows_ai"),
            }
            self._classification_queue.append(item)
            return {
                "success": True,
                "accepted": item.copy(),
                **self._classification_status_locked(now),
            }

    def clear_classifications(self) -> dict[str, Any]:
        with self._lock:
            removed = len(self._classification_queue)
            self._classification_queue.clear()
            return {
                "success": True,
                "removed": removed,
                **self._classification_status_locked(time.time()),
            }

    def classification_status(self) -> dict[str, Any]:
        with self._lock:
            return {
                "success": True,
                **self._classification_status_locked(time.time()),
            }

    def _classification_status_locked(
        self,
        now: float,
    ) -> dict[str, Any]:
        expired = self._discard_expired_classifications_locked(now)
        next_result = (
            self._classification_queue[0].copy()
            if self._classification_queue
            else None
        )
        return {
            "queue_size": len(self._classification_queue),
            "next_result": next_result,
            "expired_removed": expired,
        }

    def _discard_expired_classifications_locked(self, now: float) -> int:
        removed = 0
        while (
            self._classification_queue
            and self._classification_queue[0]["expires_at"] <= now
        ):
            self._classification_queue.popleft()
            removed += 1
        return removed

    def _peek_classification_locked(self) -> dict[str, Any] | None:
        self._discard_expired_classifications_locked(time.time())
        if not self._classification_queue:
            return None
        return self._classification_queue[0].copy()

    def _consume_classification_locked(self) -> dict[str, Any] | None:
        self._discard_expired_classifications_locked(time.time())
        if not self._classification_queue:
            return None
        return self._classification_queue.popleft()

    def status(self) -> dict[str, Any]:
        with self._lock:
            robot_state = self._get_robot_state()
            return {
                "success": True,
                "opencv_available": cv2 is not None,
                "running": self._running,
                "camera_open": self._camera_open,
                "frame_count": self._frame_count,
                "trigger_latched": self._trigger_latched,
                "stable_count": self._stable_count,
                "clear_count": self._clear_count,
                "auto_trigger": self._config.auto_trigger,
                "robot_ready": self._robot_is_ready(robot_state),
                "last_detection": self._last_detection,
                "last_trigger": self._last_trigger,
                "last_job": self._last_job,
                "last_error": self._last_error,
                "classification_queue": self._classification_status_locked(
                    time.time()
                ),
                "timing": self._calculate_timing(),
                "config": asdict(self._config),
            }

    def latest_jpeg(self) -> bytes | None:
        with self._lock:
            return self._latest_jpeg

    @staticmethod
    def _robot_is_ready(
        robot_state: dict[str, Any],
    ) -> bool:
        return (
            not bool(robot_state.get("busy", False))
            and robot_state.get("state") == "vision_ready"
        )

    def _calculate_timing(self) -> dict[str, Any]:
        config = self._config

        if config.belt_speed_mm_s <= 0:
            return {
                "arrival_ms": None,
                "start_delay_ms": 0,
                "late": False,
                "reason": "belt_speed_mm_s is zero",
            }

        arrival_ms = int(round(
            config.distance_to_pick_mm
            / config.belt_speed_mm_s
            * 1000.0
        ))
        raw_delay_ms = (
            arrival_ms
            - config.robot_time_to_grip_ms
            - config.processing_margin_ms
        )

        return {
            "arrival_ms": arrival_ms,
            "raw_start_delay_ms": raw_delay_ms,
            "start_delay_ms": max(0, raw_delay_ms),
            "late": raw_delay_ms < 0,
        }

    @staticmethod
    def _normalize_axis_angle(
        angle_deg: float,
    ) -> float:
        return angle_deg % 180.0

    @staticmethod
    def _normalize_relative_angle(
        angle_deg: float,
    ) -> float:
        return (angle_deg + 90.0) % 180.0 - 90.0

    def _calculate_wrist_angle(
        self,
        object_angle_deg: float,
    ) -> tuple[float, float]:
        config = self._config
        relative = self._normalize_relative_angle(
            object_angle_deg
            - config.conveyor_angle_deg
        )

        correction = (
            config.wrist_direction * relative
            if config.use_wrist_correction
            else 0.0
        )
        correction = max(
            -config.max_wrist_correction_deg,
            min(
                config.max_wrist_correction_deg,
                correction,
            ),
        )
        wrist_angle = (
            config.wrist_reference_angle
            + correction
        )
        wrist_angle = max(
            config.wrist_min_angle,
            min(config.wrist_max_angle, wrist_angle),
        )

        return wrist_angle, relative

    def _entry_zone_rect(
        self,
        width: int,
        height: int,
        config: VisionTriggerConfig | None = None,
    ) -> tuple[int, int, int, int]:
        active_config = config or self._config
        center_x = width * active_config.entry_zone_center_x_ratio
        center_y = height * active_config.entry_zone_center_y_ratio
        half_width = width * active_config.entry_zone_width_ratio / 2.0
        half_height = height * active_config.entry_zone_height_ratio / 2.0

        return (
            max(0, int(round(center_x - half_width))),
            max(0, int(round(center_y - half_height))),
            min(width - 1, int(round(center_x + half_width))),
            min(height - 1, int(round(center_y + half_height))),
        )

    @staticmethod
    def _center_in_rect(
        center: tuple[float, float],
        rect: tuple[int, int, int, int],
    ) -> bool:
        x, y = center
        left, top, right, bottom = rect
        return left <= x <= right and top <= y <= bottom

    def _capture_loop(self) -> None:
        config = self._config
        capture = cv2.VideoCapture(config.camera_index)

        try:
            capture.set(
                cv2.CAP_PROP_FRAME_WIDTH,
                config.frame_width,
            )
            capture.set(
                cv2.CAP_PROP_FRAME_HEIGHT,
                config.frame_height,
            )

            if not capture.isOpened():
                raise RuntimeError(
                    f"Cannot open camera index {config.camera_index}"
                )

            subtractor = cv2.createBackgroundSubtractorMOG2(
                history=config.background_history,
                varThreshold=config.background_threshold,
                detectShadows=False,
            )
            kernel = cv2.getStructuringElement(
                cv2.MORPH_ELLIPSE,
                (5, 5),
            )

            with self._lock:
                self._camera_open = True

            while not self._stop_event.is_set():
                success, frame = capture.read()
                if not success or frame is None:
                    with self._lock:
                        self._last_error = "Cannot read camera frame"
                    time.sleep(0.1)
                    continue

                config = VisionTriggerConfig(
                    **self.get_config()
                )
                detection = self._process_frame(
                    frame,
                    subtractor,
                    kernel,
                    config,
                )
                self._update_detection_state(
                    detection,
                    config,
                )
                self._update_debug_frame(
                    frame,
                    detection,
                    config,
                )

        except Exception as exc:
            logger.exception("Vision trigger stopped")
            with self._lock:
                self._last_error = str(exc)
        finally:
            capture.release()
            with self._lock:
                self._camera_open = False
                self._running = False

    def _process_frame(
        self,
        frame: Any,
        subtractor: Any,
        kernel: Any,
        config: VisionTriggerConfig,
    ) -> dict[str, Any] | None:
        height, width = frame.shape[:2]
        mask = subtractor.apply(frame)

        entry_rect = self._entry_zone_rect(
            width,
            height,
            config,
        )
        entry_zone_mask = np.zeros_like(mask)
        cv2.rectangle(
            entry_zone_mask,
            (entry_rect[0], entry_rect[1]),
            (entry_rect[2], entry_rect[3]),
            255,
            thickness=-1,
        )
        mask = cv2.bitwise_and(mask, entry_zone_mask)
        mask = cv2.morphologyEx(
            mask,
            cv2.MORPH_OPEN,
            kernel,
            iterations=1,
        )
        mask = cv2.morphologyEx(
            mask,
            cv2.MORPH_CLOSE,
            kernel,
            iterations=2,
        )

        with self._lock:
            self._frame_count += 1
            frame_count = self._frame_count

        if frame_count <= config.warmup_frames:
            return None

        contours, _ = cv2.findContours(
            mask,
            cv2.RETR_EXTERNAL,
            cv2.CHAIN_APPROX_SIMPLE,
        )
        roi_width = entry_rect[2] - entry_rect[0]
        roi_height = entry_rect[3] - entry_rect[1]
        max_area = (
            roi_width
            * roi_height
            * config.max_contour_area_ratio
        )
        candidates = [
            contour
            for contour in contours
            if (
                config.min_contour_area
                <= cv2.contourArea(contour)
                <= max_area
            )
        ]

        if not candidates:
            return None

        candidates.sort(
            key=cv2.contourArea,
            reverse=True,
        )

        for contour in candidates:
            rotated_rect = cv2.minAreaRect(contour)
            center = (
                float(rotated_rect[0][0]),
                float(rotated_rect[0][1]),
            )

            if not self._center_in_rect(center, entry_rect):
                continue

            size = rotated_rect[1]
            object_angle = float(rotated_rect[2])
            if size[0] < size[1]:
                object_angle += 90.0
            object_angle = self._normalize_axis_angle(
                object_angle
            )

            wrist_angle, relative_angle = (
                self._calculate_wrist_angle(object_angle)
            )
            x, y, box_width, box_height = cv2.boundingRect(
                contour
            )

            return {
                "center_x": round(center[0], 1),
                "center_y": round(center[1], 1),
                "area": round(
                    float(cv2.contourArea(contour)),
                    1,
                ),
                "bbox": {
                    "x": x,
                    "y": y,
                    "width": box_width,
                    "height": box_height,
                },
                "object_angle_deg": round(
                    object_angle,
                    2,
                ),
                "relative_angle_deg": round(
                    relative_angle,
                    2,
                ),
                "wrist_angle": round(wrist_angle, 1),
                "rotated_box": cv2.boxPoints(
                    rotated_rect
                ).astype(int).tolist(),
                "detected_at": time.time(),
            }

        return None

    def _same_object(
        self,
        detection: dict[str, Any],
    ) -> bool:
        center = (
            float(detection["center_x"]),
            float(detection["center_y"]),
        )
        previous = self._previous_center
        self._previous_center = center

        if previous is None:
            return True

        delta_x = center[0] - previous[0]
        delta_y = center[1] - previous[1]
        return delta_x * delta_x + delta_y * delta_y <= 6400

    def _update_detection_state(
        self,
        detection: dict[str, Any] | None,
        config: VisionTriggerConfig,
    ) -> None:
        robot_state = self._get_robot_state()
        robot_ready = self._robot_is_ready(robot_state)

        with self._lock:
            self._last_detection = detection

            if detection is None:
                self._stable_count = 0
                self._previous_center = None

                last_class = (
                    self._last_trigger.get("class_name")
                    if self._last_trigger
                    else None
                )

                if self._trigger_latched and (
                    not config.auto_trigger
                    or robot_ready
                    or last_class == "normal"
                ):
                    self._clear_count += 1

                    if (
                        self._clear_count
                        >= config.clear_frames_to_rearm
                    ):
                        self._trigger_latched = False
                        self._clear_count = 0
                return

            self._clear_count = 0

            if self._trigger_latched:
                return

            if not self._same_object(detection):
                self._stable_count = 1
            else:
                self._stable_count += 1

            if self._stable_count < config.stable_frames:
                return

            classification = self._peek_classification_locked()

            if config.auto_trigger and classification is None:
                self._last_error = (
                    "Trigger waiting: no Windows AI classification is queued"
                )
                return

            selected_class = (
                classification["class_name"]
                if classification is not None
                else "awaiting_ai"
            )

            if (
                config.auto_trigger
                and selected_class != "normal"
                and not robot_ready
            ):
                self._last_error = (
                    "Trigger waiting: robot is not at VISION_HOME"
                )
                return

            self._trigger_latched = True
            timing = self._calculate_timing()
            consumed = (
                self._consume_classification_locked()
                if config.auto_trigger
                else classification
            )
            trigger = {
                **detection,
                "class_name": selected_class,
                "inspection_id": (
                    consumed.get("inspection_id") if consumed else None
                ),
                "confidence": (
                    consumed.get("confidence") if consumed else None
                ),
                "action": (
                    consumed.get("action") if consumed else "monitor_only"
                ),
                "start_delay_ms": timing["start_delay_ms"],
                "arrival_ms": timing["arrival_ms"],
                "late": timing["late"],
                "auto_trigger": config.auto_trigger,
                "triggered_at": time.time(),
            }
            self._last_trigger = trigger

        if not config.auto_trigger:
            logger.info(
                "Vision trigger detected in monitor-only mode: %s",
                trigger,
            )
            return

        if (
            selected_class != "normal"
            and timing["late"]
            and config.reject_late_trigger
        ):
            with self._lock:
                self._last_error = (
                    "Trigger is late: move camera upstream, "
                    "slow the conveyor or stop it"
                )
            return

        try:
            job = self._on_trigger(trigger)
            with self._lock:
                self._last_job = job
                self._last_error = None
        except Exception as exc:
            logger.exception("Cannot enqueue vision pick")
            with self._lock:
                self._last_error = str(exc)

    def _update_debug_frame(
        self,
        frame: Any,
        detection: dict[str, Any] | None,
        config: VisionTriggerConfig,
    ) -> None:
        height, width = frame.shape[:2]
        entry_rect = self._entry_zone_rect(
            width,
            height,
            config,
        )
        cv2.rectangle(
            frame,
            (entry_rect[0], entry_rect[1]),
            (entry_rect[2], entry_rect[3]),
            (0, 220, 255),
            2,
        )
        cv2.putText(
            frame,
            "ENTRY / DETECTION ZONE",
            (entry_rect[0] + 8, max(24, entry_rect[1] + 24)),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.5,
            (0, 220, 255),
            2,
        )

        if detection is not None:
            points = np.array(
                detection["rotated_box"],
                dtype=np.int32,
            )
            cv2.polylines(
                frame,
                [points],
                True,
                (0, 255, 0),
                2,
            )
            label = (
                f"angle={detection['relative_angle_deg']:.1f} "
                f"wrist={detection['wrist_angle']:.1f}"
            )
            cv2.putText(
                frame,
                label,
                (
                    int(detection["center_x"]),
                    max(20, int(detection["center_y"]) - 12),
                ),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.55,
                (0, 255, 0),
                2,
            )

        mode = (
            "AUTO"
            if config.auto_trigger
            else "MONITOR ONLY"
        )
        cv2.putText(
            frame,
            mode,
            (12, 28),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.7,
            (0, 0, 255)
            if config.auto_trigger
            else (0, 220, 255),
            2,
        )

        success, encoded = cv2.imencode(
            ".jpg",
            frame,
            [int(cv2.IMWRITE_JPEG_QUALITY), 85],
        )
        if success:
            with self._lock:
                self._latest_jpeg = encoded.tobytes()


def register_vision_routes(
    app: Flask,
    controller: VisionTriggerController,
) -> None:
    @app.get("/vision/status")
    def vision_status():
        return jsonify(controller.status())

    @app.get("/vision/config")
    def vision_config():
        return jsonify({
            "success": True,
            "config": controller.get_config(),
        })

    @app.post("/vision/config")
    def update_vision_config():
        data = request.get_json(silent=True) or {}

        try:
            config = controller.update_config(data)
            return jsonify({
                "success": True,
                "config": config,
                "restart_camera_if_index_or_size_changed": True,
            })
        except (TypeError, ValueError) as exc:
            return jsonify({
                "success": False,
                "error": str(exc),
            }), 400

    @app.post("/vision/start")
    def start_vision():
        try:
            controller.start()
            return jsonify(controller.status())
        except Exception as exc:
            return jsonify({
                "success": False,
                "error": str(exc),
            }), 500

    @app.post("/vision/stop")
    def stop_vision():
        controller.stop()
        return jsonify(controller.status())

    @app.post("/vision/reset-trigger")
    def reset_vision_trigger():
        controller.reset_trigger()
        return jsonify(controller.status())

    @app.get("/vision/classifications")
    def vision_classifications():
        return jsonify(controller.classification_status())

    @app.post("/vision/classifications")
    def submit_vision_classification():
        data = request.get_json(silent=True) or {}
        try:
            return jsonify(controller.submit_classification(data)), 202
        except (TypeError, ValueError) as exc:
            return jsonify({
                "success": False,
                "error": str(exc),
                "allowed_classes": sorted(INSPECTION_CLASS_ACTIONS),
            }), 400

    @app.delete("/vision/classifications")
    def clear_vision_classifications():
        return jsonify(controller.clear_classifications())

    @app.get("/vision/frame.jpg")
    def vision_frame():
        jpeg = controller.latest_jpeg()

        if jpeg is None:
            return jsonify({
                "success": False,
                "error": "No camera frame is available",
            }), 503

        return Response(
            jpeg,
            mimetype="image/jpeg",
            headers={
                "Cache-Control": "no-store, no-cache, must-revalidate",
            },
        )
