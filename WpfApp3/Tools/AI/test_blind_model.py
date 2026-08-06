from __future__ import annotations

import argparse
import csv
import hashlib
import json
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path

from ultralytics import YOLO


DEFAULT_MODEL = Path(
    r"D:\capstone\AIService\runs\detect\finetune_battery_v2_20260804_232921\weights\best.pt"
)
DEFAULT_SOURCE = Path(r"D:\capstone\BatteryDefect_v2\Blindtest")
DEFAULT_PROJECT = Path(r"D:\capstone\WPF\WpfApp3\TestResults")
DEFAULT_MANIFEST = Path(r"D:\capstone\BatteryDefect_v2\source_manifest.csv")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run a YOLO blind test and export an auditable report.")
    parser.add_argument("--model", type=Path, default=DEFAULT_MODEL)
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--project", type=Path, default=DEFAULT_PROJECT)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--name", default=None)
    parser.add_argument("--imgsz", type=int, default=768)
    parser.add_argument("--conf", type=float, default=0.25)
    parser.add_argument("--iou", type=float, default=0.7)
    parser.add_argument("--device", default="cpu")
    return parser.parse_args()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def manifest_hashes(path: Path) -> set[str]:
    if not path.is_file():
        return set()
    with path.open("r", newline="", encoding="utf-8-sig") as stream:
        return {row["sha256"] for row in csv.DictReader(stream) if row.get("sha256")}


def main() -> None:
    args = parse_args()
    model_path = args.model.resolve()
    source = args.source.resolve()
    project = args.project.resolve()
    if not model_path.is_file():
        raise FileNotFoundError(model_path)
    if not source.is_dir():
        raise FileNotFoundError(source)

    run_name = args.name or f"blindtest_v2_{datetime.now():%Y%m%d_%H%M%S}"
    run_dir = project / run_name
    if run_dir.exists():
        raise FileExistsError(run_dir)

    known_hashes = manifest_hashes(args.manifest)
    image_extensions = {".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"}
    source_images = sorted(path for path in source.iterdir() if path.suffix.lower() in image_extensions)
    overlaps = [str(path) for path in source_images if sha256(path) in known_hashes]

    model = YOLO(str(model_path))
    results = model.predict(
        source=str(source),
        project=str(project),
        name=run_name,
        imgsz=args.imgsz,
        conf=args.conf,
        iou=args.iou,
        device=args.device,
        save=True,
        save_txt=True,
        save_conf=True,
        exist_ok=False,
        verbose=True,
    )

    rows: list[dict] = []
    class_counts: Counter[str] = Counter()
    images_by_class: dict[str, set[str]] = defaultdict(set)
    confidence_by_class: dict[str, list[float]] = defaultdict(list)
    images_without_battery: list[str] = []
    images_without_detection: list[str] = []

    for result in results:
        image_name = Path(result.path).name
        detected_classes: set[str] = set()
        if result.boxes is None or len(result.boxes) == 0:
            images_without_detection.append(image_name)
        else:
            for box in result.boxes:
                class_id = int(box.cls.item())
                confidence = float(box.conf.item())
                xyxy = [float(value) for value in box.xyxy[0].tolist()]
                class_name = result.names[class_id]
                detected_classes.add(class_name)
                class_counts[class_name] += 1
                images_by_class[class_name].add(image_name)
                confidence_by_class[class_name].append(confidence)
                rows.append({
                    "image": image_name,
                    "class_id": class_id,
                    "class_name": class_name,
                    "confidence": f"{confidence:.6f}",
                    "x1": f"{xyxy[0]:.2f}",
                    "y1": f"{xyxy[1]:.2f}",
                    "x2": f"{xyxy[2]:.2f}",
                    "y2": f"{xyxy[3]:.2f}",
                })
        if "battery" not in detected_classes:
            images_without_battery.append(image_name)

    with (run_dir / "predictions.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        fieldnames = ["image", "class_id", "class_name", "confidence", "x1", "y1", "x2", "y2"]
        writer = csv.DictWriter(stream, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    summary = {
        "model": str(model_path),
        "source": str(source),
        "output": str(run_dir),
        "settings": {"imgsz": args.imgsz, "conf": args.conf, "iou": args.iou, "device": args.device},
        "image_count": len(source_images),
        "training_overlap_count": len(overlaps),
        "training_overlap_images": overlaps,
        "total_detections": len(rows),
        "detections_by_class": dict(class_counts),
        "images_containing_class": {name: len(images) for name, images in images_by_class.items()},
        "mean_confidence_by_class": {
            name: round(sum(values) / len(values), 6) for name, values in confidence_by_class.items()
        },
        "images_without_battery": images_without_battery,
        "images_without_any_detection": images_without_detection,
        "accuracy_note": "No ground-truth labels were supplied, so accuracy, precision, recall and mAP cannot be calculated.",
    }
    (run_dir / "blindtest_summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
