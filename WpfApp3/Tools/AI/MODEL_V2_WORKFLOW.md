# Battery Inspection Model v2 Workflow

## Safety rules

- Keep `BatteryDefect`, `best_v1.pt`, and the recorded v1 metrics unchanged.
- Never overwrite a dataset or training run. Use a new versioned directory.
- Images used for v2 training are no longer blind-test images.
- Capture a new, untouched blind-test set after v2 training.
- Keep robot routing in preview/monitor mode until the new blind test passes.

## Required class order

The WPF/AI integration uses this fixed order:

```text
0 = dented
1 = battery
2 = scratched
3 = swollen
```

`prepare_dataset_v2.py` reads class names from the Roboflow `data.yaml` and
remaps IDs automatically. The order chosen by Roboflow therefore does not
need to match, but all four class names must exist exactly once.

## Annotation checklist

- Every image containing a battery has exactly one `battery` box.
- Background-only images have an empty label file.
- `dented` and `scratched` boxes tightly surround individual defects.
- Do not label printed text, wires, reflections, or battery borders as scratches.
- A battery may have multiple boxes and multiple defect classes.
- `swollen` uses one consistently defined box policy across the dataset.

## Step 1: export corrected labels

Export the corrected Roboflow project in YOLOv8 format. Do not add
augmentations during export; online augmentation is configured in the training
script.

## Step 2: create an independent v2 dataset

```powershell
Set-Location "D:\capstone\WPF\WpfApp3"

D:\capstone\AIService\venv\Scripts\python.exe `
  .\Tools\AI\prepare_dataset_v2.py `
  --source "C:\path\to\CorrectedExport.yolov8" `
  --base "D:\capstone\BatteryDefect" `
  --target "D:\capstone\BatteryDefect_v2"
```

The command fails instead of overwriting an existing target. Review
`dataset_v2_report.txt` and `hard_examples_v2_manifest.csv` before training.

## Step 3: fine-tune from frozen v1

```powershell
Set-Location "D:\capstone\AIService"

.\venv\Scripts\python.exe `
  "D:\capstone\WPF\WpfApp3\Tools\AI\finetune_v2.py"
```

The script creates a timestamped run such as
`finetune_battery_v2_20260805_120000`; it never reuses the v1 run directory.

## Step 4: acceptance checks

1. Compare v1 and v2 on the unchanged validation split.
2. Inspect per-class precision, recall, mAP50, and mAP50-95.
3. Pay special attention to recall for `dented` and `scratched`.
4. Capture and label a new blind-test set not used in either training version.
5. Test background-only images and normal batteries as hard negatives.
6. Enable automatic robot routing only after the new blind-test result is accepted.
