from __future__ import annotations

import csv
import json
import shutil
from datetime import datetime
from pathlib import Path

from ultralytics import YOLO


MODEL = Path(r"D:\capstone\AIService\runs\detect\finetune_battery_v2_20260804_232921\weights\best.pt")
SOURCE = Path(r"C:\Users\aidlsloth\Downloads\BlindTest2.yolov8\train")
PROJECT = Path(r"D:\capstone\WPF\WpfApp3\TestResults")
MANIFEST = Path(r"D:\capstone\BatteryDefect_v2\source_manifest.csv")
SOURCE_NAMES = ["battery", "dented", "scratched", "swollen"]
TARGET_NAMES = ["dented", "battery", "scratched", "swollen"]
CLASS_MAP = {0: 1, 1: 0, 2: 2, 3: 3}


def original_capture_key(stem: str) -> str:
    for marker in ("_bmp.rf.", "_png.rf.", "_jpg.rf.", "_jpeg.rf."):
        if marker in stem:
            return stem.split(marker, 1)[0]
    return stem


def main() -> None:
    if not MODEL.is_file():
        raise FileNotFoundError(MODEL)
    image_source = SOURCE / "images"
    label_source = SOURCE / "labels"
    if not image_source.is_dir() or not label_source.is_dir():
        raise FileNotFoundError(f"Expected images and labels under {SOURCE}")

    run_name = f"blindtest2_eval_{datetime.now():%Y%m%d_%H%M%S}"
    run_root = PROJECT / run_name
    dataset = run_root / "dataset"
    images = dataset / "images" / "test"
    labels = dataset / "labels" / "test"
    images.mkdir(parents=True)
    labels.mkdir(parents=True)

    image_paths = sorted(path for path in image_source.iterdir() if path.suffix.lower() in {".bmp", ".jpg", ".jpeg", ".png"})
    if not image_paths:
        raise RuntimeError("BlindTest2 contains no images")

    box_counts = {name: 0 for name in TARGET_NAMES}
    image_class_counts = {name: 0 for name in TARGET_NAMES}
    for image_path in image_paths:
        source_label = label_source / f"{image_path.stem}.txt"
        if not source_label.is_file():
            raise FileNotFoundError(source_label)
        shutil.copy2(image_path, images / image_path.name)
        output_lines: list[str] = []
        present: set[int] = set()
        for line_number, raw in enumerate(source_label.read_text(encoding="utf-8").splitlines(), 1):
            if not raw.strip():
                continue
            values = raw.split()
            if len(values) != 5:
                raise ValueError(f"{source_label}:{line_number}: expected five values")
            source_class = int(values[0])
            coords = [float(value) for value in values[1:]]
            if source_class not in CLASS_MAP or any(value < 0 or value > 1 for value in coords):
                raise ValueError(f"{source_label}:{line_number}: invalid YOLO label")
            target_class = CLASS_MAP[source_class]
            present.add(target_class)
            box_counts[TARGET_NAMES[target_class]] += 1
            output_lines.append(f"{target_class} " + " ".join(f"{value:.10f}" for value in coords))
        for target_class in present:
            image_class_counts[TARGET_NAMES[target_class]] += 1
        (labels / f"{image_path.stem}.txt").write_text("\n".join(output_lines) + "\n", encoding="utf-8")

    data_yaml = dataset / "data.yaml"
    data_yaml.write_text(
        f"path: {dataset.as_posix()}\ntrain: images/test\nval: images/test\ntest: images/test\n\n"
        + "names:\n"
        + "\n".join(f"  {index}: {name}" for index, name in enumerate(TARGET_NAMES))
        + "\n",
        encoding="utf-8",
    )

    training_keys: set[str] = set()
    if MANIFEST.is_file():
        with MANIFEST.open("r", newline="", encoding="utf-8-sig") as stream:
            for row in csv.DictReader(stream):
                training_keys.add(original_capture_key(Path(row["source_image"]).stem))
    overlaps = [path.name for path in image_paths if original_capture_key(path.stem) in training_keys]

    model = YOLO(str(MODEL))
    metrics = model.val(
        data=str(data_yaml),
        split="test",
        imgsz=768,
        batch=4,
        device="cpu",
        workers=0,
        conf=0.25,
        iou=0.7,
        plots=True,
        project=str(run_root),
        name="metrics",
    )
    model.predict(
        source=str(images),
        imgsz=768,
        conf=0.25,
        iou=0.7,
        device="cpu",
        save=True,
        save_txt=True,
        save_conf=True,
        project=str(run_root),
        name="predictions",
    )

    per_class = {}
    for class_id, name in enumerate(TARGET_NAMES):
        per_class[name] = {
            "precision": float(metrics.box.p[class_id]),
            "recall": float(metrics.box.r[class_id]),
            "mAP50": float(metrics.box.ap50[class_id]),
            "mAP50_95": float(metrics.box.ap[class_id]),
            "ground_truth_boxes": box_counts[name],
            "ground_truth_images": image_class_counts[name],
        }
    summary = {
        "model": str(MODEL),
        "source": str(SOURCE.parent),
        "output": str(run_root),
        "image_count": len(image_paths),
        "class_order_remapped": {index: name for index, name in enumerate(TARGET_NAMES)},
        "semantic_training_overlap_count": len(overlaps),
        "semantic_training_overlap_images": overlaps,
        "overall": {
            "precision": float(metrics.box.mp),
            "recall": float(metrics.box.mr),
            "mAP50": float(metrics.box.map50),
            "mAP50_95": float(metrics.box.map),
        },
        "per_class": per_class,
        "speed_ms_per_image": {key: float(value) for key, value in metrics.speed.items()},
    }
    (run_root / "evaluation_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
