# Carton Detection Training Pipeline — Module 5

Trains the **YOLOv8n** carton-detection model used by the dock vision module (Module 4). Single class: `carton`. Must run at ~5 fps burst on edge CPU / small GPU, hit **mAP50 > 0.9** and **counting accuracy > 97%**.

## Files

| File | Role |
|------|------|
| `dataset.yaml` | Ultralytics dataset config (paths, single `carton` class) |
| `train.py` | Train YOLOv8n with the augmentation strategy |
| `validate_counting.py` | Counting-accuracy metric (predicted count == GT count per image) |
| `export_onnx.py` | Export to ONNX + INT8 quantization |
| `benchmark.py` | Edge inference-latency benchmark |
| `annotation-guidelines.md` | How to label cartons-on-trays |
| `drift-monitoring.md` | Sampling + monthly re-validation plan |
| `requirements.txt` | Training deps |

## Dataset plan

- **Target: 1500 images**, top-down (matching the dock camera geometry), across:
  - lighting: bright / dim / mixed / glare
  - stacking: single layer, 2-layer, leaning
  - occlusion: partial overlap, hands/straps in frame
  - variance: tape colour, label position, carton size/brand
- **Split**: 70% train / 20% val / 10% test (`dataset.yaml`).
- **Balance**: include empty-tray and over-count (extra carton) images so counting is robust at the boundaries.

### Augmentation strategy (in `train.py`)
Brightness/contrast (±30%), motion blur, slight perspective/rotation (dock jitter), HSV shift, mosaic (early epochs only, disabled last 10 for count fidelity), horizontal flip. **No vertical flip / heavy shear** — would distort the fixed top-down geometry.

## Targets & gates

| Metric | Target | Where |
|--------|--------|-------|
| mAP50 (`carton`) | > 0.90 | `train.py` val + `val` output |
| Counting accuracy (per-image exact count) | > 0.97 | `validate_counting.py` |
| Edge latency (burst frame) | ≤ ~200 ms CPU | `benchmark.py` |

CI/release should fail if either quality gate is missed.

## Workflow

```bash
pip install -r requirements.txt
python train.py --data dataset.yaml --epochs 120
python validate_counting.py --weights runs/detect/train/weights/best.pt --data dataset.yaml
python export_onnx.py --weights runs/detect/train/weights/best.pt --int8 --data dataset.yaml
python benchmark.py --onnx runs/detect/train/weights/best.onnx
```

Copy the exported `best.onnx` to `edge/vision-module/models/carton_yolov8n.onnx`.
