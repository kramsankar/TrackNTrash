"""YOLOv8 carton detector (ONNX). Lazy-loads the model so tests can inject a fake."""
from __future__ import annotations

from typing import Protocol


class CartonDetector(Protocol):
    def count(self, frame) -> int: ...


class Yolov8OnnxDetector:
    """Runs the exported carton-detection model and counts detections above threshold."""

    def __init__(self, model_path: str, confidence: float = 0.35):
        self.model_path = model_path
        self.confidence = confidence
        self._model = None

    def _ensure(self):
        if self._model is None:
            from ultralytics import YOLO  # deferred heavy import
            self._model = YOLO(self.model_path, task="detect")

    def count(self, frame) -> int:
        self._ensure()
        results = self._model.predict(frame, conf=self.confidence, verbose=False)  # type: ignore
        if not results:
            return 0
        # single 'carton' class -> total boxes is the count
        return sum(len(r.boxes) for r in results)

    def count_burst(self, frames) -> int:
        """Use the median per-frame count to smooth transient occlusions/false boxes."""
        counts = sorted(self.count(f) for f in frames)
        if not counts:
            return 0
        return counts[len(counts) // 2]
