from __future__ import annotations

import atexit
import logging
import signal
import sys
import traceback
from typing import Any

from flask import Flask

from robot_config import (
    API_HOST,
    API_PORT,
    ENABLE_MOTION,
    validate_all_configuration,
)
from robot_jobs import start_worker, stop_worker
from robot_motion import initialize_arm
from robot_routes import register_routes
from robot_state import shutdown_event, update_state


logging.basicConfig(
    level=logging.INFO,
    format=(
        "%(asctime)s | %(levelname)s | "
        "%(threadName)s | %(message)s"
    ),
)
logger = logging.getLogger("dofbot-api")

app = Flask(__name__)
vision_controller = register_routes(app)


def initialize_robot() -> None:
    logger.info("Đang kiểm tra cấu hình pose")
    validate_all_configuration()

    logger.info("Đang khởi tạo Arm_Device")
    initialize_arm()

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
        vision_controller.stop()
    except Exception:
        logger.exception("Không thể dừng vision trigger")

    stop_worker(timeout=2.0)


def signal_handler(signum: int, _frame: Any) -> None:
    logger.info("Nhận signal %s", signum)
    shutdown_system()
    sys.exit(0)


atexit.register(shutdown_system)
signal.signal(signal.SIGINT, signal_handler)
signal.signal(signal.SIGTERM, signal_handler)


def main() -> None:
    try:
        initialize_robot()

        logger.info("=" * 60)
        logger.info("DOFBOT ROBOT API")
        logger.info("Motion enabled: %s", ENABLE_MOTION)
        logger.info("Listening on http://%s:%s", API_HOST, API_PORT)
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
