"""Dock verification pipeline orchestration: capture → quality → decode → detect → verdict."""
from __future__ import annotations

import time
from dataclasses import dataclass

from .config import ModuleConfig
from .detector import CartonDetector
from .manifest_cache import ManifestCache
from .qr_decode import decode_burst
from .verdict import DockVerification, Verdict, decide


def frame_quality(frame) -> float:
    """Laplacian variance as a sharpness proxy (higher = sharper). Rejects blur/glare."""
    try:
        import cv2
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        return float(cv2.Laplacian(gray, cv2.CV_64F).var())
    except Exception:
        return 9999.0  # if cv2 unavailable (tests), treat as sharp


class FrameSource:
    """Abstracts RTSP capture so tests can inject frames."""
    def capture_burst(self, n: int) -> list:
        raise NotImplementedError


class RtspFrameSource(FrameSource):
    def __init__(self, rtsp_url: str):
        self.rtsp_url = rtsp_url

    def capture_burst(self, n: int) -> list:
        import cv2
        cap = cv2.VideoCapture(self.rtsp_url)
        frames = []
        try:
            for _ in range(n):
                ok, frame = cap.read()
                if ok:
                    frames.append(frame)
                time.sleep(0.05)
        finally:
            cap.release()
        return frames


@dataclass
class DockPipeline:
    config: ModuleConfig
    source: FrameSource
    detector: CartonDetector
    manifests: ManifestCache

    def run_once(self, client_event_id: str | None = None) -> tuple[DockVerification, list]:
        """Execute one verification. Returns the verdict event and the frames used
        (so the caller can annotate/upload a frame on non-PASS)."""
        frames = self._quality_burst()

        decode = decode_burst(frames) if frames else decode_burst([])
        detected = self.detector.count_burst(frames) if frames else 0  # type: ignore[attr-defined]
        expected = self.manifests.expected_for(decode.tray_qr)

        verdict = decide(decode.decoded_count, detected, expected)

        event = DockVerification(
            tray_qr=decode.tray_qr,
            decoded_cartons=decode.carton_payloads,
            detected_count=detected,
            expected_count=expected,
            verdict=verdict,
            client_event_id=client_event_id,
        )
        return event, frames

    def _quality_burst(self) -> list:
        """Capture bursts until at least one sharp frame is present, up to max retries."""
        for attempt in range(self.config.max_burst_retries):
            frames = self.source.capture_burst(self.config.burst_frames)
            sharp = [f for f in frames if frame_quality(f) >= self.config.quality_threshold]
            if sharp:
                return sharp
        return []
