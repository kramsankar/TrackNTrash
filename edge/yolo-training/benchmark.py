"""Benchmark ONNX inference latency for edge planning (target ≤ ~200 ms/frame CPU → 5 fps burst).

Uses onnxruntime directly (what the edge module runs). Reports mean / p50 / p95 over N runs.

Usage:
    python benchmark.py --onnx runs/detect/carton/weights/best.onnx --runs 100 --imgsz 640
"""
import argparse
import time

import numpy as np


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--onnx", required=True)
    ap.add_argument("--runs", type=int, default=100)
    ap.add_argument("--warmup", type=int, default=10)
    ap.add_argument("--imgsz", type=int, default=640)
    ap.add_argument("--providers", default="CPUExecutionProvider")
    args = ap.parse_args()

    import onnxruntime as ort

    providers = [p.strip() for p in args.providers.split(",")]
    sess = ort.InferenceSession(args.onnx, providers=providers)
    inp = sess.get_inputs()[0]
    # NCHW float32 dummy input
    x = np.random.rand(1, 3, args.imgsz, args.imgsz).astype(np.float32)
    name = inp.name

    for _ in range(args.warmup):
        sess.run(None, {name: x})

    times = []
    for _ in range(args.runs):
        t0 = time.perf_counter()
        sess.run(None, {name: x})
        times.append((time.perf_counter() - t0) * 1000.0)

    times.sort()
    mean = sum(times) / len(times)
    p50 = times[len(times) // 2]
    p95 = times[int(len(times) * 0.95)]
    fps = 1000.0 / mean if mean else 0.0
    print(f"Providers: {providers}")
    print(f"Latency ms  mean={mean:.1f}  p50={p50:.1f}  p95={p95:.1f}  ({fps:.1f} fps)")
    if mean > 200.0:
        print("WARNING: mean latency > 200 ms — may not sustain 5 fps burst on this device.")


if __name__ == "__main__":
    main()
