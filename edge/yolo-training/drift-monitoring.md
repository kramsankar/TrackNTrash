# Drift Monitoring Plan

Detect and correct model degradation once the dock camera is live.

## Sampling

- **1% of PASS frames** sampled to Blob `pass-samples/{yyyy}/{MM}/{dd}/` (lifecycle: 30 days).
- **100% of non-PASS frames** already retained in `exceptions/` (1 year) — these are the hard cases.
- Sampling is done in the edge module (deterministic hash on frame id → 1%) so it adds negligible load.

## Signals (operational drift)

Track weekly, alert on > 2σ deviation from the rolling 8-week baseline:

| Signal | Meaning if it moves |
|--------|--------------------|
| PASS rate ↓ | Lighting/camera change, or real quality drop |
| Mean verification time ↑ | Occlusion/retries up, or hardware degradation |
| `UNKNOWN_CARTON` rate ↑ | QR print/quality issue or new carton style the detector misses |
| Manual-override rate ↑ (ops console) | Model disagreeing with humans → retrain signal |

## Monthly re-validation

1. Pull the month's exception frames + the 1% PASS sample.
2. A labeler annotates a **fresh 300-image validation set** from these (real distribution, not synthetic).
3. Run `validate_counting.py` against the live model on this set.
4. If counting accuracy < 0.97 **or** mAP50 < 0.90 → schedule a retrain that folds the new hard cases into the training set.

## Retrain loop

```
exceptions + pass-sample  →  label hard cases  →  add to datasets/cartons
   →  train.py (gates)  →  export_onnx.py (INT8)  →  benchmark.py
   →  deploy new model via module twin `modelPath` (no image rebuild if mounted)
```

Version each model (`carton_yolov8n_vN.onnx`) and record its validation metrics; keep the previous version for one-command rollback.
