"""Module configuration, backed by IoT Edge module-twin desired properties."""
from __future__ import annotations

from dataclasses import dataclass


@dataclass
class ModuleConfig:
    camera_rtsp_url: str = "rtsp://dock-cam.local/stream1"
    burst_frames: int = 5
    quality_threshold: float = 100.0        # Laplacian variance; higher = sharper
    max_burst_retries: int = 3
    # Base of the deployed tracking API. manifest_sync_url is derived from it so the
    # two cannot drift apart and point at different environments.
    api_base_url: str = "https://app-tracking-tracktrash-dev-z3yo3x.azurewebsites.net"
    manifest_sync_url: str = ""
    camera_code: str = "CAM-DOCK-1"
    heartbeat_seconds: int = 60
    blob_exception_container: str = "exceptions"
    gpio_relay_pin: int = 17
    trigger_mode: str = "motion"            # "motion" | "button"
    # Absolute, because the model is bind-mounted from the gateway rather than baked
    # into the image; a relative path would silently depend on the working directory.
    model_path: str = "/app/models/carton_yolov8n.onnx"
    confidence: float = 0.35
    # Non-maximum suppression overlap. Ours since the detector dropped ultralytics, and
    # worth tuning per dock: tightly packed cartons need a higher value or neighbours get
    # merged, loosely spaced ones a lower one or one carton counts twice.
    iou_threshold: float = 0.45

    @classmethod
    def from_twin(cls, desired: dict) -> "ModuleConfig":
        cfg = cls()
        mapping = {
            "cameraRtspUrl": "camera_rtsp_url",
            "burstFrames": "burst_frames",
            "qualityThreshold": "quality_threshold",
            "maxBurstRetries": "max_burst_retries",
            "apiBaseUrl": "api_base_url",
            "manifestSyncUrl": "manifest_sync_url",
            "cameraCode": "camera_code",
            "heartbeatSeconds": "heartbeat_seconds",
            "blobExceptionContainer": "blob_exception_container",
            "gpioRelayPin": "gpio_relay_pin",
            "triggerMode": "trigger_mode",
            "modelPath": "model_path",
            "confidence": "confidence",
            "iouThreshold": "iou_threshold",
        }
        for twin_key, attr in mapping.items():
            if twin_key in desired and desired[twin_key] is not None:
                setattr(cfg, attr, desired[twin_key])
        # __post_init__ has already derived a sync URL from the default host, so pointing
        # the twin at another environment would otherwise still sync from the old one.
        # An explicit manifestSyncUrl in the twin still wins.
        if "manifestSyncUrl" not in desired:
            cfg.manifest_sync_url = cfg._derived_manifest_url()
        return cfg

    def __post_init__(self) -> None:
        if not self.manifest_sync_url:
            self.manifest_sync_url = self._derived_manifest_url()

    def _derived_manifest_url(self) -> str:
        return f"{self.api_base_url.rstrip('/')}/manifests"
