"""Local tray-manifest cache with delta sync from the tracking API."""
from __future__ import annotations

import time
from dataclasses import dataclass, field
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .api_auth import ApiAuth


@dataclass
class ManifestCache:
    sync_url: str
    # The manifest endpoint is no longer anonymous; without this the sync returns 401 and
    # the dock silently verifies every tray against an empty expected-count table.
    auth: "ApiAuth | None" = None
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

            def http_get(url, headers=None):  # noqa: E306
                r = requests.get(url, headers=headers or {}, timeout=10)
                # One forced re-sign-in covers the token expiring between calls; a second
                # 401 means the credentials are wrong, and retrying will not fix that.
                if r.status_code == 401 and self.auth is not None:
                    r = requests.get(url, headers=self.auth.headers(force=True), timeout=10)
                r.raise_for_status()
                return r.json()

        url = f"{self.sync_url}?since={self._last_sync_iso}"
        headers = self.auth.headers() if self.auth is not None else {}
        try:
            data = http_get(url, headers=headers)
        except TypeError:
            # Tests inject a single-argument http_get; keep that contract working.
            data = http_get(url)
        manifests = data.get("manifests", []) if isinstance(data, dict) else []
        for m in manifests:
            self.upsert(m["trayQr"], int(m.get("expectedCartonCount", 0)))
        # advance the watermark (server returns UTC; use its `since` echo or now)
        self._last_sync_iso = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
        return len(manifests)
