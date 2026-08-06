from __future__ import annotations

import argparse
import csv
import hashlib
import shutil
from collections import Counter
from pathlib import Path

import yaml


TARGET_CLASSES = ["dented", "battery", "scratched", "swollen"]
IMAGE_EXTENSIONS = {".bmp", ".jpg", ".jpeg", ".png"}
SOURCE_SPLITS = ("train", "valid", "val", "test")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Create a clean BatteryDefect_v2 dataset from the existing base "
            "dataset and a corrected Roboflow YOLOv8 export."
        )
    )
    parser.add_argument(
        "--source",
        type=Path,
        required=True,
        help="Root of the corrected Roboflow YOLOv8 export.",
    )
    parser.add_argument(
        "--base",
        type=Path,
        default=Path(r"D:\capstone\BatteryDefect"),
        help="Existing v1 dataset. It is read only.",
    )
    parser.add_argument(
        "--target",
        type=Path,
        default=Path(r"D:\capstone\BatteryDefect_v2"),
        help="New dataset directory. It must not already exist.",
    )
    return parser.parse_args()


def normalize_names(raw_names: object) -> dict[int, str]:
    if isinstance(raw_names, list):
        names = {index: str(name).strip().lower() for index, name in enumerate(raw_names)}
    elif isinstance(raw_names, dict):
        names = {
            int(index): str(name).strip().lower()
            for index, name in raw_names.items()
        }
    else:
        raise ValueError("data.yaml must contain names as a list or mapping")

    if set(names.values()) != set(TARGET_CLASSES):
        raise ValueError(
            f"Expected exactly {TARGET_CLASSES}, found {dict(sorted(names.items()))}"
        )
    if len(names) != len(TARGET_CLASSES):
        raise ValueError("Class names must be unique")
    return names


def find_yaml(source: Path) -> Path:
    preferred = source / "data.yaml"
    if preferred.is_file():
        return preferred
    candidates = sorted(source.glob("*.yaml")) + sorted(source.glob("*.yml"))
    if len(candidates) != 1:
        raise FileNotFoundError(
            f"Expected one data.yaml in {source}; found {len(candidates)} YAML files"
        )
    return candidates[0]


def discover_source_pairs(source: Path) -> list[tuple[str, Path, Path]]:
    pairs: list[tuple[str, Path, Path]] = []
    discovered_stems: set[str] = set()

    for split in SOURCE_SPLITS:
        image_dir = source / split / "images"
        label_dir = source / split / "labels"
        if not image_dir.is_dir():
            continue
        if not label_dir.is_dir():
            raise FileNotFoundError(f"Labels directory is missing: {label_dir}")

        images = sorted(
            path
            for path in image_dir.iterdir()
            if path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS
        )
        image_stems = {path.stem for path in images}
        label_stems = {path.stem for path in label_dir.glob("*.txt")}
        if image_stems != label_stems:
            raise ValueError(
                f"{split}: image/label mismatch; "
                f"missing labels={sorted(image_stems-label_stems)[:5]}, "
                f"orphan labels={sorted(label_stems-image_stems)[:5]}"
            )

        for image_path in images:
            unique_key = f"{split}/{image_path.stem}"
            if unique_key in discovered_stems:
                raise ValueError(f"Duplicate source entry: {unique_key}")
            discovered_stems.add(unique_key)
            pairs.append((split, image_path, label_dir / f"{image_path.stem}.txt"))

    if not pairs:
        raise RuntimeError(f"No image/label pairs found in {source}")
    return pairs


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_and_remap_labels(
    label_path: Path,
    source_names: dict[int, str],
) -> tuple[list[str], Counter[str], list[str]]:
    output_lines: list[str] = []
    counts: Counter[str] = Counter()
    warnings: list[str] = []

    for line_number, line in enumerate(
        label_path.read_text(encoding="utf-8").splitlines(), 1
    ):
        if not line.strip():
            continue
        fields = line.split()
        if len(fields) != 5:
            raise ValueError(f"{label_path}:{line_number}: expected 5 fields")
        try:
            source_id = int(fields[0])
            coordinates = [float(value) for value in fields[1:]]
        except ValueError as exc:
            raise ValueError(f"{label_path}:{line_number}: invalid numeric value") from exc
        if source_id not in source_names:
            raise ValueError(f"{label_path}:{line_number}: unknown class {source_id}")
        if not all(0.0 <= value <= 1.0 for value in coordinates):
            raise ValueError(f"{label_path}:{line_number}: coordinates outside 0..1")
        if coordinates[2] <= 0.0 or coordinates[3] <= 0.0:
            raise ValueError(f"{label_path}:{line_number}: non-positive box size")

        class_name = source_names[source_id]
        target_id = TARGET_CLASSES.index(class_name)
        area = coordinates[2] * coordinates[3]
        if class_name in {"dented", "scratched"} and area > 0.20:
            warnings.append(
                f"{label_path.name}:{line_number} unusually large "
                f"{class_name} box (normalized area={area:.3f})"
            )
        counts[class_name] += 1
        output_lines.append(" ".join([str(target_id), *fields[1:]]))

    if counts and counts["battery"] != 1:
        raise ValueError(
            f"{label_path}: every labeled battery image must contain exactly "
            f"one battery box; found {counts['battery']}"
        )
    return output_lines, counts, warnings


def copy_base_split(base: Path, target: Path, split: str) -> None:
    for category in ("images", "labels"):
        source_dir = base / category / split
        target_dir = target / category / split
        if not source_dir.is_dir():
            raise FileNotFoundError(f"Base split is missing: {source_dir}")
        shutil.copytree(source_dir, target_dir)


def audit_final_dataset(target: Path) -> dict[str, dict[str, object]]:
    report: dict[str, dict[str, object]] = {}
    hashes_by_split: dict[str, set[str]] = {}

    for split in ("train", "val"):
        images = sorted(
            path
            for path in (target / "images" / split).iterdir()
            if path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS
        )
        labels = sorted((target / "labels" / split).glob("*.txt"))
        if {path.stem for path in images} != {path.stem for path in labels}:
            raise RuntimeError(f"Final {split} image/label pairs do not match")

        box_counts: Counter[int] = Counter()
        image_presence: Counter[int] = Counter()
        split_hashes: set[str] = set()
        for image_path in images:
            digest = sha256(image_path)
            if digest in split_hashes:
                raise RuntimeError(f"Exact duplicate inside {split}: {image_path}")
            split_hashes.add(digest)
            present: set[int] = set()
            for line in (target / "labels" / split / f"{image_path.stem}.txt").read_text(
                encoding="utf-8"
            ).splitlines():
                if not line.strip():
                    continue
                class_id = int(line.split()[0])
                if class_id not in range(len(TARGET_CLASSES)):
                    raise RuntimeError(f"Invalid final class ID {class_id}")
                box_counts[class_id] += 1
                present.add(class_id)
            image_presence.update(present)

        hashes_by_split[split] = split_hashes
        report[split] = {
            "images": len(images),
            "labels": len(labels),
            "box_counts": dict(sorted(box_counts.items())),
            "image_presence": dict(sorted(image_presence.items())),
        }

    overlap = hashes_by_split["train"].intersection(hashes_by_split["val"])
    if overlap:
        raise RuntimeError(f"Found {len(overlap)} exact image duplicates across train/val")
    return report


def main() -> None:
    args = parse_args()
    source = args.source.resolve()
    base = args.base.resolve()
    target = args.target.resolve()
    if not source.is_dir():
        raise FileNotFoundError(f"Corrected export does not exist: {source}")
    if not base.is_dir():
        raise FileNotFoundError(f"Base dataset does not exist: {base}")
    if target.exists():
        raise FileExistsError(
            f"Target already exists: {target}. Use a new versioned path; "
            "this tool never overwrites a dataset."
        )

    yaml_path = find_yaml(source)
    source_config = yaml.safe_load(yaml_path.read_text(encoding="utf-8"))
    source_names = normalize_names(source_config.get("names"))
    remap = {
        source_id: TARGET_CLASSES.index(name)
        for source_id, name in source_names.items()
    }
    pairs = discover_source_pairs(source)

    parsed: list[dict[str, object]] = []
    source_hashes: set[str] = set()
    all_warnings: list[str] = []
    for source_split, image_path, label_path in pairs:
        digest = sha256(image_path)
        if digest in source_hashes:
            raise ValueError(f"Exact duplicate inside corrected export: {image_path}")
        source_hashes.add(digest)
        lines, counts, warnings = read_and_remap_labels(label_path, source_names)
        all_warnings.extend(warnings)
        parsed.append(
            {
                "source_split": source_split,
                "image": image_path,
                "label": label_path,
                "hash": digest,
                "lines": lines,
                "counts": counts,
                "warnings": warnings,
            }
        )

    target.mkdir(parents=True)
    try:
        copy_base_split(base, target, "train")
        copy_base_split(base, target, "val")

        base_hashes = {
            sha256(path)
            for split in ("train", "val")
            for path in (base / "images" / split).iterdir()
            if path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS
        }
        manifest_rows = []
        imported = 0
        skipped = 0
        for entry in parsed:
            image_path = entry["image"]
            assert isinstance(image_path, Path)
            if entry["hash"] in base_hashes:
                skipped += 1
                continue

            source_split = str(entry["source_split"])
            destination_name = f"hardv2__{source_split}__{image_path.name}"
            destination_image = target / "images" / "train" / destination_name
            destination_label = (
                target / "labels" / "train" / f"{Path(destination_name).stem}.txt"
            )
            shutil.copy2(image_path, destination_image)
            lines = entry["lines"]
            assert isinstance(lines, list)
            destination_label.write_text(
                "\n".join(lines) + ("\n" if lines else ""),
                encoding="utf-8",
            )
            counts = entry["counts"]
            assert isinstance(counts, Counter)
            manifest_rows.append(
                {
                    "source_split": source_split,
                    "source_image": str(image_path),
                    "destination_image": destination_name,
                    "sha256": entry["hash"],
                    **{f"{name}_boxes": counts[name] for name in TARGET_CLASSES},
                    "warnings": " | ".join(entry["warnings"]),
                }
            )
            imported += 1

        (target / "data.yaml").write_text(
            "\n".join(
                [
                    f"path: {target.as_posix()}",
                    "train: images/train",
                    "val: images/val",
                    "names:",
                    *[f"  {index}: {name}" for index, name in enumerate(TARGET_CLASSES)],
                    "",
                ]
            ),
            encoding="utf-8",
        )
        with (target / "hard_examples_v2_manifest.csv").open(
            "w", newline="", encoding="utf-8-sig"
        ) as handle:
            fieldnames = list(manifest_rows[0].keys()) if manifest_rows else ["source_image"]
            writer = csv.DictWriter(handle, fieldnames=fieldnames)
            writer.writeheader()
            writer.writerows(manifest_rows)

        final_report = audit_final_dataset(target)
        (target / "dataset_v2_report.txt").write_text(
            "\n".join(
                [
                    f"source={source}",
                    f"base={base}",
                    f"target={target}",
                    f"source_names={dict(sorted(source_names.items()))}",
                    f"class_remap={dict(sorted(remap.items()))}",
                    f"imported={imported}",
                    f"skipped_exact_duplicates={skipped}",
                    f"warnings={len(all_warnings)}",
                    *[f"warning: {warning}" for warning in all_warnings],
                    f"final_report={final_report}",
                    "",
                ]
            ),
            encoding="utf-8",
        )
    except Exception:
        shutil.rmtree(target, ignore_errors=True)
        raise

    print(f"Dataset v2 created: {target}")
    print(f"Source class order: {dict(sorted(source_names.items()))}")
    print(f"Class remap: {dict(sorted(remap.items()))}")
    print(f"Imported hard-example images: {imported}")
    print(f"Skipped exact duplicates: {skipped}")
    print(f"Warnings: {len(all_warnings)}")
    print(f"Final audit: {final_report}")


if __name__ == "__main__":
    main()
