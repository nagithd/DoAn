from __future__ import annotations

import csv
import hashlib
import json
import shutil
from collections import Counter, defaultdict
from pathlib import Path


TARGET = Path(r"D:\capstone\BatteryDefect_v2")
SOURCES = {
    "addition2": Path(r"C:\Users\aidlsloth\Downloads\Addition_2.yolov8"),
    "batterydefect": Path(r"C:\Users\aidlsloth\Downloads\BatteryDefect.yolov8"),
    "addition": Path(r"C:\Users\aidlsloth\Downloads\Addition.yolov8"),
    "blindtest": Path(r"C:\Users\aidlsloth\Downloads\BlindTest.yolov8"),
}

# All four Roboflow exports use this order. The output order matches the model/API.
SOURCE_NAMES = ["battery", "dented", "scratched", "swollen"]
TARGET_NAMES = ["dented", "battery", "scratched", "swollen"]
CLASS_MAP = {i: TARGET_NAMES.index(name) for i, name in enumerate(SOURCE_NAMES)}
IMAGE_EXTENSIONS = {".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"}

BAD_BLIND_STEM = "Image_20260804111729219_bmp.rf.fPaNY5aJFnnr7tQ8edsf"
BAD_TINY_STEM = "Image_20260803172630350_bmp.rf.zkZOWtedtCj9Ep6rd0Sx"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def parse_label(path: Path) -> list[list[float]]:
    boxes: list[list[float]] = []
    for line_number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not raw.strip():
            continue
        values = raw.split()
        if len(values) != 5:
            raise ValueError(f"{path}:{line_number}: expected 5 values")
        class_id = int(values[0])
        coords = [float(value) for value in values[1:]]
        if class_id not in CLASS_MAP or any(value < 0 or value > 1 for value in coords):
            raise ValueError(f"{path}:{line_number}: invalid YOLO annotation")
        boxes.append([float(class_id), *coords])
    return boxes


def repair_boxes(source: str, stem: str, boxes: list[list[float]]) -> tuple[list[list[float]], list[str]]:
    repaired: list[list[float]] = []
    notes: list[str] = []
    for index, box in enumerate(boxes):
        class_id, _, _, width, height = box
        area = width * height

        # A 1-pixel-like battery box is an accidental click, not an object.
        if source == "batterydefect" and stem == BAD_TINY_STEM and class_id == 0 and area < 0.0001:
            notes.append("removed accidental tiny battery box")
            continue

        # This old blind-test annotation covers the complete battery, but was classed as scratched.
        if source == "blindtest" and stem == BAD_BLIND_STEM and index == 0 and class_id == 2 and area > 0.4:
            box = [0.0, *box[1:]]
            notes.append("reclassified full-battery box: scratched -> battery")

        repaired.append(box)
    return repaired, notes


def output_split(source: str, original_split: str) -> str:
    # Keep Addition_2's independently exported validation subset; hard examples and older
    # datasets are used for training. Exact duplicates are removed globally below.
    return "val" if source == "addition2" and original_split in {"valid", "val"} else "train"


def main() -> None:
    if TARGET.exists():
        raise FileExistsError(f"Refusing to overwrite existing target: {TARGET}")

    records: list[dict] = []
    for source, root in SOURCES.items():
        if not root.is_dir():
            raise FileNotFoundError(root)
        for original_split in ("train", "valid", "val", "test"):
            image_dir = root / original_split / "images"
            label_dir = root / original_split / "labels"
            if not image_dir.is_dir():
                continue
            for image_path in sorted(path for path in image_dir.iterdir() if path.suffix.lower() in IMAGE_EXTENSIONS):
                label_path = label_dir / f"{image_path.stem}.txt"
                if not label_path.is_file():
                    raise FileNotFoundError(f"Missing label for {image_path}")
                boxes, repair_notes = repair_boxes(source, image_path.stem, parse_label(label_path))
                records.append({
                    "source": source,
                    "original_split": original_split,
                    "split": output_split(source, original_split),
                    "image": image_path,
                    "label": label_path,
                    "boxes": boxes,
                    "sha256": sha256(image_path),
                    "repairs": "; ".join(repair_notes),
                })

    # Prefer validation for duplicate resolution, otherwise follow source insertion order.
    kept: list[dict] = []
    duplicates: list[dict] = []
    by_hash: dict[str, dict] = {}
    for record in sorted(records, key=lambda item: 0 if item["split"] == "val" else 1):
        previous = by_hash.get(record["sha256"])
        if previous is not None:
            duplicates.append({
                "removed": str(record["image"]),
                "kept": str(previous["image"]),
                "sha256": record["sha256"],
            })
            continue
        by_hash[record["sha256"]] = record
        kept.append(record)

    for split in ("train", "val"):
        (TARGET / "images" / split).mkdir(parents=True)
        (TARGET / "labels" / split).mkdir(parents=True)

    manifest_rows: list[dict] = []
    class_boxes: dict[str, Counter] = defaultdict(Counter)
    class_images: dict[str, Counter] = defaultdict(Counter)
    repairs: list[dict] = []
    used_names: set[str] = set()

    for record in sorted(kept, key=lambda item: (item["split"], item["source"], item["image"].name)):
        image_path: Path = record["image"]
        safe_stem = f"{record['source']}__{image_path.stem}"
        if safe_stem.lower() in used_names:
            safe_stem += f"__{record['sha256'][:10]}"
        used_names.add(safe_stem.lower())
        destination_image = TARGET / "images" / record["split"] / f"{safe_stem}{image_path.suffix.lower()}"
        destination_label = TARGET / "labels" / record["split"] / f"{safe_stem}.txt"
        shutil.copy2(image_path, destination_image)

        output_lines: list[str] = []
        image_classes: set[int] = set()
        for box in record["boxes"]:
            source_class = int(box[0])
            target_class = CLASS_MAP[source_class]
            image_classes.add(target_class)
            class_boxes[record["split"]][target_class] += 1
            output_lines.append(f"{target_class} " + " ".join(f"{value:.10f}" for value in box[1:]))
        for target_class in image_classes:
            class_images[record["split"]][target_class] += 1
        destination_label.write_text("\n".join(output_lines) + ("\n" if output_lines else ""), encoding="utf-8")

        if record["repairs"]:
            repairs.append({"source_image": str(image_path), "changes": record["repairs"]})
        manifest_rows.append({
            "split": record["split"],
            "output_image": str(destination_image.relative_to(TARGET)),
            "source": record["source"],
            "source_split": record["original_split"],
            "source_image": str(image_path),
            "sha256": record["sha256"],
            "box_count": len(record["boxes"]),
            "repairs": record["repairs"],
        })

    (TARGET / "data.yaml").write_text(
        f"path: {TARGET.as_posix()}\ntrain: images/train\nval: images/val\n\nnames:\n"
        + "\n".join(f"  {index}: {name}" for index, name in enumerate(TARGET_NAMES))
        + "\n",
        encoding="utf-8",
    )
    with (TARGET / "source_manifest.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(manifest_rows[0]))
        writer.writeheader()
        writer.writerows(manifest_rows)

    split_counts = Counter(row["split"] for row in manifest_rows)
    report = {
        "target": str(TARGET),
        "source_records": len(records),
        "unique_images": len(manifest_rows),
        "images": dict(split_counts),
        "target_class_order": {index: name for index, name in enumerate(TARGET_NAMES)},
        "boxes_by_split": {
            split: {TARGET_NAMES[index]: count for index, count in sorted(counter.items())}
            for split, counter in class_boxes.items()
        },
        "images_containing_class_by_split": {
            split: {TARGET_NAMES[index]: count for index, count in sorted(counter.items())}
            for split, counter in class_images.items()
        },
        "duplicates_removed": duplicates,
        "label_repairs": repairs,
    }
    (TARGET / "dataset_report.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")

    # Final independent integrity checks.
    for split in ("train", "val"):
        images = list((TARGET / "images" / split).iterdir())
        labels = list((TARGET / "labels" / split).glob("*.txt"))
        if len(images) != len(labels):
            raise RuntimeError(f"Image/label mismatch in {split}")
        image_stems = {path.stem for path in images}
        label_stems = {path.stem for path in labels}
        if image_stems != label_stems:
            raise RuntimeError(f"Unpaired files in {split}")
    train_hashes = {sha256(path) for path in (TARGET / "images" / "train").iterdir()}
    val_hashes = {sha256(path) for path in (TARGET / "images" / "val").iterdir()}
    if train_hashes & val_hashes:
        raise RuntimeError("Exact image leakage between train and val")

    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
