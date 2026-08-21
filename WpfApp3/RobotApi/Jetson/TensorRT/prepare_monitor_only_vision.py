#!/usr/bin/env python3
"""Generate a hard-locked YOLO monitor-only vision_trigger.py.

The project normally keeps DOFBOT vision processing disabled.  This utility
creates a deployable copy that enables TensorRT inference while forcing
monitor_only=True and auto_trigger=False, including when an old
vision_config.json or an HTTP config update asks for automatic routing.
It never edits the source file in place.
"""

import argparse
import py_compile
from pathlib import Path


PROCESSING_DISABLED = "VISION_PROCESSING_ENABLED = False"
PROCESSING_ENABLED = "VISION_PROCESSING_ENABLED = True"

LOAD_GUARD = """        # A stale config file must never re-enable camera-driven robot motion
        # after this monitor-only detector is deployed.
        if config.monitor_only:
            config.auto_trigger = False
"""

LOAD_HARD_LOCK = """        # Hard safety lock for the optional TensorRT camera test.  Camera
        # detections may be displayed, but they must never route robot motion.
        config.monitor_only = True
        config.auto_trigger = False
"""

UPDATE_GUARD = """            self._apply_config_values(updated, values)
            if updated.monitor_only:
                updated.auto_trigger = False
"""

UPDATE_HARD_LOCK = """            self._apply_config_values(updated, values)
            # Ignore attempts to enable camera-driven routing while this
            # generated monitor-only build is installed.
            updated.monitor_only = True
            updated.auto_trigger = False
"""


def replace_exactly_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise RuntimeError(
            f"Expected exactly one {label} marker, found {count}. "
            "The source layout has changed; inspect it instead of applying "
            "an unsafe automatic patch."
        )
    return text.replace(old, new, 1)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_arguments()
    source = args.source.resolve()
    output = args.output.resolve()
    text = source.read_text(encoding="utf-8")

    text = replace_exactly_once(
        text,
        PROCESSING_DISABLED,
        PROCESSING_ENABLED,
        "VISION_PROCESSING_ENABLED",
    )
    text = replace_exactly_once(
        text,
        LOAD_GUARD,
        LOAD_HARD_LOCK,
        "config-load monitor guard",
    )
    text = replace_exactly_once(
        text,
        UPDATE_GUARD,
        UPDATE_HARD_LOCK,
        "config-update monitor guard",
    )

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(text, encoding="utf-8", newline="\n")
    py_compile.compile(str(output), doraise=True)
    print(f"Generated monitor-only vision module: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
