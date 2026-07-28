"""Multi-frame QR decoding. Uses zxing-cpp when available, falls back to pyzbar.

Heavy imports are deferred so the pure union logic (in verdict.py) stays importable anywhere.
"""
from __future__ import annotations

from typing import Sequence

from .verdict import DecodeResult, union_qr_codes


def _decode_frame(frame) -> list[str]:
    """Decode all QR payloads in a single frame (numpy BGR image)."""
    try:
        import zxingcpp  # type: ignore
        results = zxingcpp.read_barcodes(frame)
        return [r.text for r in results if r.text]
    except Exception:
        pass
    try:
        from pyzbar.pyzbar import decode as zbar_decode  # type: ignore
        return [d.data.decode("utf-8", "ignore") for d in zbar_decode(frame)]
    except Exception:
        return []


def decode_burst(frames: Sequence) -> DecodeResult:
    """Decode every frame in the burst and union the results (occlusion-robust)."""
    per_frame = [_decode_frame(f) for f in frames]
    return union_qr_codes(per_frame)
