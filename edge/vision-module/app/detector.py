"""YOLOv8 carton detector on onnxruntime.

This used to load the model through ultralytics, which drags in torch and made the module
image 3.1 GB — a painful pull over a warehouse network, for a dependency only the *export*
step genuinely needs. Inference needs an ONNX session and some array arithmetic.

The cost of dropping ultralytics is that its postprocessing has to be done here: decode the
raw output, threshold, and suppress overlapping boxes. That arithmetic is the part most likely
to be subtly wrong, so it lives in small module-level functions that are unit-tested against
synthetic tensors rather than hidden inside the class.
"""
from __future__ import annotations

from typing import Protocol

import numpy as np


class CartonDetector(Protocol):
    def count(self, frame) -> int: ...


class ModelMissing(RuntimeError):
    """The ONNX model is not where the module was told to look.

    Raised with the mount in the message because that is nearly always the cause: the model
    is mounted from the host rather than baked into the image, so an unmounted volume or a
    file dropped in the wrong directory presents identically to a corrupt install.
    """


# --------------------------------------------------------------------------- geometry

def letterbox(frame, size: int = 640):
    """Resize preserving aspect ratio and pad to a square.

    Squashing to a square instead would distort the cartons away from the shapes the model was
    trained on, which costs accuracy for no saving. Returns the padded image plus the ratio and
    padding, so boxes can be mapped back to source pixels.
    """
    import cv2  # deferred: the postprocessing tests do not need it

    h, w = frame.shape[:2]
    if h == 0 or w == 0:
        raise ValueError("empty frame")
    ratio = min(size / h, size / w)
    nh, nw = int(round(h * ratio)), int(round(w * ratio))
    resized = cv2.resize(frame, (nw, nh), interpolation=cv2.INTER_LINEAR)

    canvas = np.full((size, size, 3), 114, dtype=np.uint8)   # 114 = YOLO's pad grey
    top, left = (size - nh) // 2, (size - nw) // 2
    canvas[top:top + nh, left:left + nw] = resized
    return canvas, ratio, (left, top)


def preprocess(frame, size: int = 640) -> np.ndarray:
    """Frame (BGR uint8) -> NCHW float32 RGB in 0..1, as the export expects."""
    padded, _, _ = letterbox(frame, size)
    rgb = padded[:, :, ::-1]                                  # BGR -> RGB
    chw = np.ascontiguousarray(rgb.transpose(2, 0, 1), dtype=np.float32) / 255.0
    return chw[None, ...]


def xywh_to_xyxy(boxes: np.ndarray) -> np.ndarray:
    """Centre-form (cx, cy, w, h) -> corner-form (x1, y1, x2, y2)."""
    out = np.empty(boxes.shape, dtype=np.float32)
    half_w, half_h = boxes[:, 2] / 2.0, boxes[:, 3] / 2.0
    out[:, 0] = boxes[:, 0] - half_w
    out[:, 1] = boxes[:, 1] - half_h
    out[:, 2] = boxes[:, 0] + half_w
    out[:, 3] = boxes[:, 1] + half_h
    return out


def nms(boxes: np.ndarray, scores: np.ndarray, iou_threshold: float = 0.45) -> list[int]:
    """Greedy non-maximum suppression. Returns kept indices, highest score first.

    Without this, one carton yields several overlapping boxes and the dock count reads high,
    which surfaces as a spurious OVER exception — worse than no count at all, because it
    teaches people to ignore the board.
    """
    if len(boxes) == 0:
        return []

    x1, y1, x2, y2 = boxes[:, 0], boxes[:, 1], boxes[:, 2], boxes[:, 3]
    areas = np.clip(x2 - x1, 0, None) * np.clip(y2 - y1, 0, None)
    order = scores.argsort()[::-1]

    keep: list[int] = []
    while order.size > 0:
        i = int(order[0])
        keep.append(i)
        if order.size == 1:
            break
        rest = order[1:]

        xx1 = np.maximum(x1[i], x1[rest])
        yy1 = np.maximum(y1[i], y1[rest])
        xx2 = np.minimum(x2[i], x2[rest])
        yy2 = np.minimum(y2[i], y2[rest])
        inter = np.clip(xx2 - xx1, 0, None) * np.clip(yy2 - yy1, 0, None)
        union = areas[i] + areas[rest] - inter
        # A zero-area box would divide by zero; treat it as non-overlapping.
        iou = np.where(union > 0, inter / np.maximum(union, 1e-9), 0.0)

        order = rest[iou <= iou_threshold]
    return keep


# --------------------------------------------------------------------------- decoding

def decode(output, confidence: float, iou_threshold: float = 0.45):
    """Raw ONNX output -> (boxes_xyxy, scores) after thresholding and suppression.

    Handles the two export shapes that turn up in practice:

    * plain export    -> (1, 4 + num_classes, anchors); boxes are centre-form and the anchor
      axis is last, so it needs transposing.
    * export with NMS -> (1, detections, 6) as x1, y1, x2, y2, score, class — already decoded,
      so only the threshold applies.

    Guessing wrong between them silently produces a plausible but meaningless count, so the
    shape is inspected rather than assumed.
    """
    arr = np.asarray(output, dtype=np.float32)
    if arr.ndim == 3:
        arr = arr[0]
    if arr.ndim != 2:
        raise ValueError(f"unexpected detector output shape: {np.asarray(output).shape}")

    # End-to-end export: last axis is exactly x1,y1,x2,y2,score,class.
    if arr.shape[1] == 6 and arr.shape[0] != 6:
        boxes, scores = arr[:, :4], arr[:, 4]
        keep = scores >= confidence
        return boxes[keep], scores[keep]

    # Plain export. The anchor count (8400 at 640px) dwarfs 4+nc, so the long axis is anchors.
    if arr.shape[0] < arr.shape[1]:
        arr = arr.T

    if arr.shape[1] < 5:
        raise ValueError(f"detector output has too few channels: {arr.shape}")

    boxes_xywh = arr[:, :4]
    scores = arr[:, 4:].max(axis=1)

    keep = scores >= confidence
    if not keep.any():
        return np.empty((0, 4), dtype=np.float32), np.empty((0,), dtype=np.float32)

    boxes = xywh_to_xyxy(boxes_xywh[keep])
    scores = scores[keep]

    kept = nms(boxes, scores, iou_threshold)
    return boxes[kept], scores[kept]


# --------------------------------------------------------------------------- detector

class Yolov8OnnxDetector:
    """Counts cartons in a frame using an exported YOLOv8 ONNX model."""

    def __init__(self, model_path: str, confidence: float = 0.35,
                 iou_threshold: float = 0.45, input_size: int = 640,
                 threads: int = 2):
        self.model_path = model_path
        self.confidence = confidence
        self.iou_threshold = iou_threshold
        self.input_size = input_size
        self.threads = threads
        self._session = None
        self._input_name = None

    def model_available(self) -> bool:
        """True when the model file is present and non-empty."""
        import os
        try:
            return os.path.isfile(self.model_path) and os.path.getsize(self.model_path) > 0
        except OSError:
            return False

    def _ensure(self):
        if self._session is not None:
            return

        # Check before handing the path to onnxruntime, whose error for a missing file names
        # only the path — no help at all when the real cause is an unmounted volume.
        if not self.model_available():
            raise ModelMissing(
                f"detection model not found at {self.model_path!r}. "
                "The model is mounted from the host, not baked into the image: put the "
                "exported carton_yolov8n.onnx in /var/lib/tracktrash/models on the gateway "
                "and confirm the module's Binds entry maps it to /app/models."
            )

        import onnxruntime as ort  # deferred so the module imports without the runtime

        opts = ort.SessionOptions()
        # The gateway also runs edgeHub and the capture loop. Letting the session take every
        # core makes frame capture stutter and drop frames mid-burst.
        opts.intra_op_num_threads = self.threads
        opts.inter_op_num_threads = 1
        opts.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL

        available = ort.get_available_providers()
        providers = [p for p in ("CUDAExecutionProvider", "CPUExecutionProvider") if p in available]
        self._session = ort.InferenceSession(self.model_path, sess_options=opts, providers=providers)
        self._input_name = self._session.get_inputs()[0].name

    def detect(self, frame):
        """Returns (boxes_xyxy, scores) in letterboxed input space."""
        self._ensure()
        tensor = preprocess(frame, self.input_size)
        outputs = self._session.run(None, {self._input_name: tensor})  # type: ignore[union-attr]
        return decode(outputs[0], self.confidence, self.iou_threshold)

    def count(self, frame) -> int:
        boxes, _ = self.detect(frame)
        return int(len(boxes))

    def count_burst(self, frames) -> int:
        """Use the median per-frame count to smooth transient occlusions/false boxes."""
        counts = sorted(self.count(f) for f in frames)
        if not counts:
            return 0
        return counts[len(counts) // 2]
