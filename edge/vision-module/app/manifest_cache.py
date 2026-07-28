"""Local tray-manifest cache with delta sync from the tracking API."""
from __future__ import annotations

import time
from dataclasses import dataclass, field


@dataclass
class ManifestCache:
    sync_url: str
    _by_tray: dict[str, int] = field(default_factory=dict)   # trayQr -> expectedCartonCount
    _last_sync_iso: str = "2000-01-01T00:00:00Z"

    def expected_for(self, tray_qr: str | None) -> int | None:
        if tray_qr is None:
            return None
        return self._by_tray.get(tray_qr)

    def upsert(self, tray_qr: str, expected: int) -> None:
        self._by_tray[tray_qr] = expected

    def sync(self, http_get=None) -> int:
        """Pull manifests changed since the last sync. `http_get` is injectable for tests.

        Returns the number of manifests updated.
        """
        if http_get is None:
            import requests  # deferred
            def http_get(url):  # noqa: E306
                return requests.get(url, timeout=10).json()

        data = http_get(f"{self.sync_url}?since={self._last_sync_iso}")
        manifests = data.get("manifests", []) if isinstance(data, dict) else []
        for m in manifests:
            self.upsert(m["trayQr"], int(m.get("expectedCartonCount", 0)))
        # advance the watermark (server returns UTC; use its `since` echo or now)
        self._last_sync_iso = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
        return len(manifests)
