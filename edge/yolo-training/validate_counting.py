"""Counting-accuracy metric: fraction of images where predicted carton count == GT count.

This is the metric that matters at the dock — mAP alone can be high while counts are off by one.
Gate: > 0.97.

Usage:
    python validate_counting.py --weights runs/detect/carton/weights/best.pt --data dataset.yaml --split test
"""
import argparse
import glob
import os

import yaml


def count_gt_labels(label_path: str) -> int:
    if not os.path.exists(label_path):
        return 0
    with open(label_path) as f:
        return sum(1 for line in f if line.strip())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--weights", required=True)
    ap.add_argument("--data", default="dataset.yaml")
    ap.add_argument("--split", default="test", choices=["train", "val", "test"])
    ap.add_argument("--conf", type=float, default=0.35)
    ap.add_argument("--gate", type=float, default=0.97)
    args = ap.parse_args()

    with open(args.data) as f:
        cfg = yaml.safe_load(f)
    root = cfg["path"]
    img_dir = os.path.join(root, cfg[args.split])
    lbl_dir = img_dir.replace("images", "labels")

    from ultralytics import YOLO
    model = YOLO(args.weights)

    images = sorted(glob.glob(os.path.join(img_dir, "*.jpg")) + glob.glob(os.path.join(img_dir, "*.png")))
    if not images:
        raise SystemExit(f"No images found in {img_dir}")

    exact = 0
    off_by = {}
    for img in images:
        stem = os.path.splitext(os.path.basename(img))[0]
        gt = count_gt_labels(os.path.join(lbl_dir, stem + ".txt"))
        pred = model.predict(img, conf=args.conf, verbose=False)
        pred_count = sum(len(r.boxes) for r in pred)
        if pred_count == gt:
            exact += 1
        else:
            off_by[stem] = (gt, pred_count)

    accuracy = exact / len(images)
    print(f"Counting accuracy ({args.split}): {accuracy:.4f} over {len(images)} images")
    if off_by:
        print(f"  {len(off_by)} miscounts (showing up to 10):")
        for stem, (gt, pred) in list(off_by.items())[:10]:
            print(f"    {stem}: gt={gt} pred={pred}")

    if accuracy < args.gate:
        raise SystemExit(f"QUALITY GATE FAILED: counting accuracy {accuracy:.4f} < {args.gate}")
    print("Counting-accuracy gate passed.")


if __name__ == "__main__":
    main()
