"""Optional camera admission check for calibrated, fixed-pose checkpoint picks.

No pixel-to-joint conversion is guessed. Enable only after measuring the region
reachable by PICK_DOWN: PICK_GUARD_ENABLED=1 and PICK_GUARD_REGION=l,t,r,b
(normalized image coordinates). Defaults preserve existing fixed-pose operation.
"""
from __future__ import annotations

import math
import os
import time
from typing import Callable

ENABLED = os.environ.get("PICK_GUARD_ENABLED", "0") == "1"
REGION = os.environ.get("PICK_GUARD_REGION", "")


class StableTarget:
    def __init__(self, region, min_confidence=0.60, frames=5,
                 stable_seconds=0.5, max_age=1.0, radius_pixels=5.0):
        if len(region) != 4 or not all(math.isfinite(v) for v in region):
            raise ValueError("PICK_GUARD_REGION must contain four finite ratios")
        left, top, right, bottom = region
        if not (0 <= left < right <= 1 and 0 <= top < bottom <= 1):
            raise ValueError("PICK_GUARD_REGION requires 0<=left<right<=1 and 0<=top<bottom<=1")
        self.region = region
        self.min_confidence = min_confidence
        self.frames = frames
        self.stable_seconds = stable_seconds
        self.max_age = max_age
        self.radius_pixels = radius_pixels
        self.last_stamp = -1.0
        self.samples = []

    def observe(self, detection, now, after):
        if not detection:
            self.samples = []
            return False
        try:
            stamp = float(detection["capture_monotonic"])
            x, y = float(detection["center_x"]), float(detection["center_y"])
            w, h = float(detection["frame_width"]), float(detection["frame_height"])
            confidence = float(detection["confidence"])
            values = [stamp, x, y, w, h, confidence]
            if not all(math.isfinite(v) for v in values): raise ValueError("nonfinite")
            l, t, r, b = self.region
            valid = (stamp >= after and 0 <= now-stamp <= self.max_age and
                     w > 0 and h > 0 and self.min_confidence <= confidence <= 1 and
                     l <= x/w <= r and t <= y/h <= b)
        except (KeyError, ValueError, TypeError, ZeroDivisionError):
            valid = False
        if not valid:
            self.samples = []
            return False
        if stamp <= self.last_stamp: return False
        self.last_stamp = stamp
        # Every sample must stay close to all previous samples, not just the
        # preceding frame (which would incorrectly accept a slowly moving pin).
        if any(math.hypot(x-p[1], y-p[2]) > self.radius_pixels or
               stamp-p[0] > self.max_age or (w,h) != p[3:5] for p in self.samples):
            self.samples = []
        self.samples.append((stamp, x, y, w, h))
        return len(self.samples) >= self.frames and stamp-self.samples[0][0] >= self.stable_seconds


def wait_for_target(snapshot: Callable, shutdown_event, timeout=10.0):
    if not ENABLED:
        return {"mode": "disabled", "verified": False}
    try:
        region = tuple(float(v) for v in REGION.split(','))
        target = StableTarget(region)
    except ValueError as exc:
        raise RuntimeError("Calibrate PICK_GUARD_REGION before enabling camera admission") from exc
    started = time.monotonic()
    while time.monotonic()-started < timeout:
        if shutdown_event.is_set(): raise RuntimeError("Shutdown while waiting for target")
        state = snapshot()
        if not state["running"] or not state["camera_open"]:
            raise RuntimeError("DOFBOT camera is not running; conveyor remains held")
        detection = state["last_detection"]
        if target.observe(detection, time.monotonic(), started):
            return {"mode": "camera_fixed_pose", "verified": True,
                    "center_x": detection["center_x"], "center_y": detection["center_y"],
                    "confidence": detection["confidence"], "frames": len(target.samples)}
        shutdown_event.wait(0.03)
    raise RuntimeError("No fresh stable target in calibrated pick region within 10 seconds")
