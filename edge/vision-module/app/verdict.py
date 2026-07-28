"""Pure verdict + QR-union logic for the dock verification pipeline.

Kept free of heavy dependencies (cv2 / ultralytics / zxing) so it is trivially unit-testable.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Iterable


class Verdict(str, Enum):
    PASS = "PASS"
    COUNT_MISMATCH = "COUNT_MISMATCH"
    UNKNOWN_CARTON = "UNKNOWN_CARTON"
    MISSING_CARTON = "MISSING_CARTON"
    UNKNOWN = "UNKNOWN"


@dataclass
class DecodeResult:
    """Union of QR codes decoded across a burst of frames."""
    tray_qr: str | None
    carton_payloads: list[str] = field(default_factory=list)

    @property
    def decoded_count(self) -> int:
        return len(self.carton_payloads)


def union_qr_codes(per_frame_payloads: Iterable[Iterable[str]]) -> DecodeResult:
    """Union QR payloads across frames; classify tray vs carton codes.

    Tray codes start with 'TRAY-' or 'MANIFEST-'; everything else is treated as a carton.
    A code hidden by occlusion in one frame may appear in another — the union recovers it.
    """
    trays: set[str] = set()
    cartons: set[str] = set()
    for frame in per_frame_payloads:
        for code in frame:
            if not code:
                continue
            if code.startswith("TRAY-") or code.startswith("MANIFEST-"):
                trays.add(code)
            else:
                cartons.add(code)
    tray_qr = sorted(trays)[0] if trays else None
    return DecodeResult(tray_qr=tray_qr, carton_payloads=sorted(cartons))


def decide(decoded_count: int, detected_count: int, expected_count: int | None) -> Verdict:
    """Compare decoded QR count, YOLO-detected carton count, and expected manifest count.

    Rules (see README):
      * all three equal                      -> PASS
      * no manifest available                -> UNKNOWN (cannot verify)
      * physical boxes exceed readable QRs    -> UNKNOWN_CARTON (a box with no scannable code)
      * fewer decoded or detected than expected -> MISSING_CARTON
      * otherwise counts disagree            -> COUNT_MISMATCH
    """
    if expected_count is None:
        return Verdict.UNKNOWN

    if decoded_count == detected_count == expected_count:
        return Verdict.PASS

    if detected_count > decoded_count:
        return Verdict.UNKNOWN_CARTON

    if decoded_count < expected_count or detected_count < expected_count:
        return Verdict.MISSING_CARTON

    return Verdict.COUNT_MISMATCH


@dataclass
class DockVerification:
    """The event emitted to IoT Hub."""
    tray_qr: str | None
    decoded_cartons: list[str]
    detected_count: int
    expected_count: int | None
    verdict: Verdict
    frame_ref: str | None = None
    client_event_id: str | None = None

    def to_message(self) -> dict:
        return {
            "eventType": "DockVerification",
            "trayQr": self.tray_qr,
            "decodedCartons": self.decoded_cartons,
            "decodedCount": len(self.decoded_cartons),
            "detectedCount": self.detected_count,
            "expectedCount": self.expected_count,
            "verdict": self.verdict.value,
            "frameRef": self.frame_ref,
            "clientEventId": self.client_event_id,
        }
