"""Camera heartbeat — tells the API this camera is alive.

Without it a working camera reads as offline on the console, and a genuinely dead one
looks the same as one that simply has nothing to report.
"""
from __future__ import annotations

from dataclasses import dataclass


@dataclass
class Heartbeat:
    api_base: str
    camera_code: str
    auth: "object | None" = None

    def send(self, http_post=None) -> bool:
        """Posts one heartbeat. Returns False rather than raising — a missed heartbeat
        must never take down the verification pipeline that is the camera's actual job."""
        url = f"{self.api_base.rstrip('/')}/cameras/{self.camera_code}/heartbeat"
        headers = self.auth.headers() if self.auth is not None else {}

        if http_post is None:
            import requests  # deferred

            def http_post(u, headers):  # noqa: E306
                r = requests.post(u, json={}, headers=headers, timeout=10)
                if r.status_code == 401 and self.auth is not None:
                    r = requests.post(u, json={}, headers=self.auth.headers(force=True), timeout=10)
                return r.status_code

        try:
            return 200 <= http_post(url, headers) < 300
        except Exception as ex:
            print(f"[camera] heartbeat failed: {ex}")
            return False
