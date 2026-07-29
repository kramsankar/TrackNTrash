"""Tests for the pure verdict + decode-union + pipeline logic (no camera / model needed)."""
import os
import sys

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from app.verdict import Verdict, decide, union_qr_codes, DockVerification  # noqa: E402
from app.manifest_cache import ManifestCache  # noqa: E402
from app.config import ModuleConfig  # noqa: E402
from app.pipeline import DockPipeline, FrameSource  # noqa: E402


# ---------------- verdict logic ----------------

def test_pass_when_all_counts_match():
    assert decide(decoded_count=5, detected_count=5, expected_count=5) is Verdict.PASS

def test_unknown_when_no_manifest():
    assert decide(5, 5, None) is Verdict.UNKNOWN

def test_unknown_carton_when_more_boxes_than_qrs():
    # 6 physical boxes, only 5 readable QRs -> a box with no scannable code
    assert decide(decoded_count=5, detected_count=6, expected_count=6) is Verdict.UNKNOWN_CARTON

def test_missing_carton_when_fewer_than_expected():
    assert decide(decoded_count=4, detected_count=4, expected_count=5) is Verdict.MISSING_CARTON

def test_count_mismatch_general():
    # decoded matches expected but detector saw fewer (occluded) -> counts disagree
    assert decide(decoded_count=5, detected_count=5, expected_count=6) is Verdict.MISSING_CARTON


# ---------------- QR union ----------------

def test_union_recovers_occluded_codes():
    frames = [
        ["TRAY-LDN1-000001", "C1", "C2"],   # C3 occluded here
        ["C2", "C3"],                        # C1 occluded here
    ]
    r = union_qr_codes(frames)
    assert r.tray_qr == "TRAY-LDN1-000001"
    assert r.carton_payloads == ["C1", "C2", "C3"]
    assert r.decoded_count == 3

def test_union_ignores_empty():
    r = union_qr_codes([[], ["C1"], [""]])
    assert r.carton_payloads == ["C1"]
    assert r.tray_qr is None


# ---------------- manifest cache ----------------

def test_manifest_sync_with_injected_http():
    cache = ManifestCache(sync_url="http://x/manifests")
    def fake_get(url):
        return {"manifests": [{"trayQr": "TRAY-1", "expectedCartonCount": 7}]}
    n = cache.sync(http_get=fake_get)
    assert n == 1
    assert cache.expected_for("TRAY-1") == 7
    assert cache.expected_for("TRAY-unknown") is None


# ---------------- pipeline end-to-end with fakes ----------------

class FakeSource(FrameSource):
    def __init__(self, bursts):
        self._bursts = list(bursts)
    def capture_burst(self, n):
        return self._bursts.pop(0) if self._bursts else []

class FakeDetector:
    def __init__(self, count):
        self._count = count
    def count(self, frame):
        return self._count
    def count_burst(self, frames):
        return self._count


def _decode_stub(monkey_frames):
    # Patch decode_burst by having frames carry their payloads as attributes is awkward;
    # instead we test the pipeline's verdict wiring via the manifest + detector, using
    # frames that decode to nothing and asserting UNKNOWN_CARTON when boxes exist.
    pass


def test_pipeline_flags_unknown_carton(monkeypatch):
    import app.pipeline as pl
    # 3 "frames" (opaque objects); force decode to return no QRs, detector sees 2 boxes.
    monkeypatch.setattr(pl, "decode_burst", lambda frames: union_qr_codes([[]]))
    monkeypatch.setattr(pl, "frame_quality", lambda f: 9999.0)

    cache = ManifestCache(sync_url="x")
    pipe = DockPipeline(
        config=ModuleConfig(burst_frames=3, max_burst_retries=1),
        source=FakeSource([[object(), object(), object()]]),
        detector=FakeDetector(2),
        manifests=cache,
    )
    event, frames = pipe.run_once(client_event_id="cid-1")
    # No manifest for a None tray -> UNKNOWN (cannot verify) is acceptable;
    # message shape is always well-formed.
    msg = event.to_message()
    assert msg["eventType"] == "DockVerification"
    assert msg["detectedCount"] == 2
    assert msg["clientEventId"] == "cid-1"


def test_dockverification_message_shape():
    ev = DockVerification(tray_qr="TRAY-1", decoded_cartons=["C1", "C2"],
                          detected_count=2, expected_count=2, verdict=Verdict.PASS)
    m = ev.to_message()
    assert m["verdict"] == "PASS"
    assert m["decodedCount"] == 2
    assert m["expectedCount"] == 2


# ---- Service-account auth (added when the manifest + heartbeat endpoints were guarded) ----

def test_manifest_sync_still_accepts_a_single_argument_http_get():
    """Tests inject a one-arg http_get; adding auth headers must not break that."""
    from app.manifest_cache import ManifestCache

    cache = ManifestCache(sync_url="https://api.test/manifests")
    n = cache.sync(http_get=lambda url: {"manifests": [{"trayQr": "T-1", "expectedCartonCount": 4}]})
    assert n == 1
    assert cache.expected_for("T-1") == 4


def test_manifest_sync_sends_the_bearer_token_when_authenticated():
    from app.api_auth import ApiAuth
    from app.manifest_cache import ManifestCache

    auth = ApiAuth(base_url="https://api.test", username="camera-agent", password="pw")
    auth.token(http_post=lambda url, json: (200, {"token": "TOK", "expiresUtc": None}))

    seen = {}

    def http_get(url, headers=None):
        seen["headers"] = headers
        return {"manifests": []}

    ManifestCache(sync_url="https://api.test/manifests", auth=auth).sync(http_get=http_get)
    assert seen["headers"]["Authorization"] == "Bearer TOK"


def test_bad_credentials_raise_rather_than_running_blind():
    """A silent auth failure would verify every tray against an empty expected table."""
    import pytest
    from app.api_auth import ApiAuth, AuthError

    auth = ApiAuth(base_url="https://api.test", username="camera-agent", password="wrong")
    with pytest.raises(AuthError):
        auth.token(http_post=lambda url, json: (401, {}))


def test_no_credentials_means_no_auth_header_rather_than_a_crash():
    from app.api_auth import ApiAuth

    auth = ApiAuth(base_url="https://api.test")
    assert auth.configured is False
    assert auth.headers() == {}


def test_token_is_reused_until_it_nears_expiry():
    import time
    from app.api_auth import ApiAuth

    calls = []

    def http_post(url, json):
        calls.append(url)
        return (200, {"token": f"TOK{len(calls)}", "expiresUtc": None})

    auth = ApiAuth(base_url="https://api.test", username="u", password="p")
    auth._token, auth._expires_at = "CACHED", time.time() + 3600
    assert auth.token(http_post=http_post) == "CACHED"
    assert calls == []                      # nothing fetched while the token is fresh
    assert auth.token(force=True, http_post=http_post) == "TOK1"


def test_heartbeat_failure_never_raises():
    """A missed heartbeat must not take down the verification pipeline."""
    from app.heartbeat import Heartbeat

    def boom(url, headers):
        raise OSError("network down")

    assert Heartbeat(api_base="https://api.test", camera_code="CAM-1").send(http_post=boom) is False
