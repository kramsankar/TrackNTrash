"""Bearer-token auth for the tracking API, for an unattended device.

The API used to serve the manifest delta sync and the camera heartbeat anonymously. Both
are guarded now, so the module signs in as its own service account.

Nobody is present to retype a password when a token expires, so credentials are held (from
the environment or the module twin) and a new token is fetched on demand — on first use,
when the current one is close to expiry, and once more if the server still answers 401.
"""
from __future__ import annotations

import os
import threading
import time
from dataclasses import dataclass, field


class AuthError(RuntimeError):
    """Sign-in failed for a reason retrying will not fix (bad credentials, no account)."""


@dataclass
class ApiAuth:
    base_url: str
    username: str = ""
    password: str = ""
    # Refresh a little before the server's expiry so a long call cannot straddle it.
    refresh_margin_seconds: int = 300

    _token: str | None = field(default=None, repr=False)
    _expires_at: float = 0.0
    _lock: threading.Lock = field(default_factory=threading.Lock, repr=False)

    @classmethod
    def from_env(cls, base_url: str) -> "ApiAuth":
        return cls(
            base_url=base_url.rstrip("/"),
            username=os.environ.get("TNT_API_USERNAME", ""),
            password=os.environ.get("TNT_API_PASSWORD", ""),
        )

    @property
    def configured(self) -> bool:
        return bool(self.username and self.password)

    def token(self, force: bool = False, http_post=None) -> str | None:
        """Current bearer token, signing in if needed. None when no credentials are set."""
        if not self.configured:
            return None
        with self._lock:
            fresh = self._token and time.time() < self._expires_at - self.refresh_margin_seconds
            if fresh and not force:
                return self._token
            self._sign_in(http_post)
            return self._token

    def headers(self, force: bool = False, http_post=None) -> dict[str, str]:
        tok = self.token(force=force, http_post=http_post)
        return {"Authorization": f"Bearer {tok}"} if tok else {}

    def _sign_in(self, http_post=None) -> None:
        if http_post is None:
            import requests  # deferred; not present in the test environment

            def http_post(url, json):  # noqa: E306
                r = requests.post(url, json=json, timeout=15)
                return r.status_code, (r.json() if r.content else {})

        status, body = http_post(
            f"{self.base_url}/auth/login",
            {"username": self.username, "password": self.password},
        )
        if status == 401:
            raise AuthError("tracking API rejected the camera service account credentials")
        if status != 200 or not body.get("token"):
            raise AuthError(f"sign-in failed: HTTP {status}")

        self._token = body["token"]
        # Trust the server's expiry when it parses; otherwise assume a short life so the
        # next call refreshes rather than riding a token that may already be dead.
        self._expires_at = _parse_expiry(body.get("expiresUtc")) or (time.time() + 600)


def _parse_expiry(value: str | None) -> float | None:
    if not value:
        return None
    try:
        from datetime import datetime

        text = value.replace("Z", "+00:00")
        # Fractional seconds can exceed six digits, which fromisoformat rejects.
        if "." in text:
            head, _, tail = text.partition(".")
            digits = "".join(c for c in tail if c.isdigit())[:6]
            offset = tail[len(tail) - 6:] if ("+" in tail or "-" in tail) else ""
            text = f"{head}.{digits}{offset}"
        return datetime.fromisoformat(text).timestamp()
    except Exception:
        return None
