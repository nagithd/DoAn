from __future__ import annotations

import argparse
from datetime import datetime
from pathlib import Path

import torch
from ultralytics import YOLO


DEFAULT_DATA = Path(r"D:\capstone\BatteryDefect_v2\data.yaml")
DEFAULT_MODEL = Path(
    r"D:\capstone\AIService\models\battery-inspection-v1\best.pt"
)
DEFAULT_PROJECT = Path(r"D:\capstone\AIService\runs\detect")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Fine-tune battery-inspection-v2 from the frozen v1 checkpoint."
    )
    parser.add_argument("--data", type=Path, default=DEFAULT_DATA)
    parser.add_argument("--model", type=Path, default=DEFAULT_MODEL)
    parser.add_argument("--project", type=Path, default=DEFAULT_PROJECT)
    parser.add_argument("--epochs", type=int, default=50)
    parser.add_argument("--imgsz", type=int, default=768)
    parser.add_argument("--batch", type=int, default=None)
    parser.add_argument("--device", default=None, help="Examples: cpu, 0, 0,1")
    parser.add_argument("--name", default=None)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    data = args.data.resolve()
    model_path = args.model.resolve()
    project = args.project.resolve()
    if not data.is_file():
        raise FileNotFoundError(
            f"Dataset v2 is not ready: {data}. Run prepare_dataset_v2.py first."
        )
    if not model_path.is_file():
        raise FileNotFoundError(f"Frozen v1 checkpoint not found: {model_path}")

    cuda_available = torch.cuda.is_available()
    device = args.device if args.device is not None else (0 if cuda_available else "cpu")
    batch = args.batch if args.batch is not None else (8 if cuda_available else 4)
    run_name = args.name or f"finetune_battery_v2_{datetime.now():%Y%m%d_%H%M%S}"
    run_dir = project / run_name
    if run_dir.exists():
        raise FileExistsError(f"Training run already exists: {run_dir}")

    print("=" * 76)
    print(f"Dataset       : {data}")
    print(f"Frozen v1    : {model_path}")
    print(f"Output        : {run_dir}")
    print(f"Device        : {device}")
    print(f"Image size    : {args.imgsz}")
    print(f"Batch         : {batch}")
    print("Class order   : 0=dented, 1=battery, 2=scratched, 3=swollen")
    print("=" * 76)

    model = YOLO(str(model_path))
    model.train(
        data=str(data),
        project=str(project),
        name=run_name,
        epochs=args.epochs,
        patience=12,
        imgsz=args.imgsz,
        batch=batch,
        device=device,
        workers=0,
        optimizer="AdamW",
        lr0=0.00015,
        lrf=0.05,
        cos_lr=True,
        weight_decay=0.0005,
        warmup_epochs=2.0,
        degrees=5.0,
        translate=0.03,
        scale=0.12,
        fliplr=0.10,
        flipud=0.0,
        hsv_h=0.0,
        hsv_s=0.0,
        hsv_v=0.08,
        mosaic=0.05,
        mixup=0.0,
        close_mosaic=8,
        seed=42,
        deterministic=True,
        cache=False,
        plots=True,
        save=True,
        save_period=10,
        exist_ok=False,
        amp=cuda_available,
    )

    best_model = run_dir / "weights" / "best.pt"
    if not best_model.is_file():
        raise FileNotFoundError(f"Training finished but best.pt is missing: {best_model}")

    print(f"\nValidating v2 checkpoint: {best_model}")
    YOLO(str(best_model)).val(
        data=str(data),
        split="val",
        imgsz=args.imgsz,
        batch=batch,
        device=device,
        workers=0,
        plots=True,
    )
    print(f"\nV2 candidate checkpoint: {best_model}")


if __name__ == "__main__":
    main()
