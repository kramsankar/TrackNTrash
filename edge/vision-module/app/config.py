"""Module configuration, backed by IoT Edge module-twin desired properties."""
from __future__ import annotations

from dataclasses import dataclass


@dataclass
class ModuleConfig:
    camera_rtsp_url: str = "rtsp://dock-cam.local/stream1"
    burst_frames: int = 5
    quality_threshold: float = 100.0        # Laplacian variance; higher = sharper
    max_burst_retries: int = 3
    manifest_sync_url: str = "https://tracktrash-tracking.azurewebsites.net/manifests"
    blob_exception_container: str = "exceptions"
    gpio_relay_pin: int = 17
    trigger_mode: str = "motion"            # "motion" | "button"
    model_path: str = "models/carton_yolov8n.onnx"
    confidence: float = 0.35

    @classmethod
    def from_twin(cls, desired: dict) -> "ModuleConfig":
        cfg = cls()
        mapping = {
            "cameraRtspUrl": "camera_rtsp_url",
            "burstFrames": "burst_frames",
            "qualityThreshold": "quality_threshold",
            "maxBurstRetries": "max_burst_retries",
            "manifestSyncUrl": "manifest_sync_url",
            "blobExceptionContainer": "blob_exception_container",
            "gpioRelayPin": "gpio_relay_pin",
            "triggerMode": "trigger_mode",
            "modelPath": "model_path",
            "confidence": "confidence",
        }
        for twin_key, attr in mapping.items():
            if twin_key in desired and desired[twin_key] is not None:
                setattr(cfg, attr, desired[twin_key])
        return cfg
