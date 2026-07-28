"""Train YOLOv8n for carton detection.

Augmentations are tuned for a FIXED top-down dock camera: brightness/blur/perspective jitter
that reflect real dock variance, but no vertical flip / heavy shear (would break the geometry).
Mosaic is disabled for the final epochs so per-image counts stay faithful.

Usage:
    python train.py --data dataset.yaml --epochs 120 --imgsz 640
"""
import argparse


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", default="dataset.yaml")
    ap.add_argument("--epochs", type=int, default=120)
    ap.add_argument("--imgsz", type=int, default=640)
    ap.add_argument("--batch", type=int, default=16)
    ap.add_argument("--model", default="yolov8n.pt")
    ap.add_argument("--device", default="0")   # "0" GPU, "cpu" for CPU
    args = ap.parse_args()

    from ultralytics import YOLO

    model = YOLO(args.model)
    results = model.train(
        data=args.data,
        epochs=args.epochs,
        imgsz=args.imgsz,
        batch=args.batch,
        device=args.device,
        # ---- augmentation (dock-appropriate) ----
        hsv_h=0.015, hsv_s=0.5, hsv_v=0.4,     # colour / brightness / glare variance
        degrees=8.0,                            # slight rotation (tray jitter)
        translate=0.08,
        scale=0.4,
        shear=0.0,                              # keep geometry
        perspective=0.0005,                     # mild
        flipud=0.0,                             # NO vertical flip (top-down fixed)
        fliplr=0.5,
        mosaic=1.0,
        close_mosaic=10,                        # disable mosaic for the last 10 epochs (count fidelity)
        # ---- training ----
        patience=25,
        optimizer="auto",
        seed=42,
        project="runs/detect",
        name="carton",
    )

    # Enforce the mAP50 quality gate.
    metrics = model.val(data=args.data)
    map50 = float(metrics.box.map50)
    print(f"mAP50 = {map50:.4f}")
    if map50 < 0.90:
        raise SystemExit(f"QUALITY GATE FAILED: mAP50 {map50:.4f} < 0.90")
    print("mAP50 gate passed.")


if __name__ == "__main__":
    main()
