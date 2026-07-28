"""Export the trained model to ONNX, optionally INT8-quantized for edge CPU.

INT8 uses the validation images as the calibration set. Verify accuracy after quantization
(re-run validate_counting.py against the ONNX via a small wrapper if needed).

Usage:
    python export_onnx.py --weights runs/detect/carton/weights/best.pt --int8 --data dataset.yaml
"""
import argparse


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--weights", required=True)
    ap.add_argument("--data", default="dataset.yaml")
    ap.add_argument("--imgsz", type=int, default=640)
    ap.add_argument("--int8", action="store_true", help="INT8 quantize (needs calibration data via --data)")
    args = ap.parse_args()

    from ultralytics import YOLO
    model = YOLO(args.weights)

    export_kwargs = dict(format="onnx", imgsz=args.imgsz, opset=12, simplify=True, dynamic=False)
    if args.int8:
        # Ultralytics performs INT8 calibration using the dataset's val split.
        export_kwargs.update(int8=True, data=args.data)

    path = model.export(**export_kwargs)
    print(f"Exported ONNX: {path}")
    print("Copy to edge/vision-module/models/carton_yolov8n.onnx")


if __name__ == "__main__":
    main()
