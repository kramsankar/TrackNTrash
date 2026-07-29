import { useState } from "react";
import { loginLocal, type AuthConfig, type Session } from "../auth";

/** Sign-in screen. Offers only the methods the deployment reports from /auth/config. */
export function Login({ config, onSignedIn }: { config: AuthConfig; onSignedIn: (s: Session) => void }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [err, setErr] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setErr(""); setBusy(true);
    try { onSignedIn(await loginLocal(username, password)); }
    catch (ex) { setErr((ex as Error).message); }
    finally { setBusy(false); }
  }

  function entraSignIn() {
    // MSAL redirect flow: install @azure/msal-browser and redirect to the authority.
    // Config comes from the API so the console needs no build-time Entra settings.
    setErr("Entra ID sign-in is configured on the API but the MSAL redirect is not wired in this build. Use username & password.");
  }

  return (
    <div className="login-shell">
      <div className="login-card">
        <div className="login-brand">
          <div className="login-mark">◧</div>
          <div>
            <h1>TrackNTrash</h1>
            <p>Dispatch Track &amp; Trace</p>
          </div>
        </div>

        <h2 className="login-title">Sign in</h2>

        {config.local && (
          <form onSubmit={submit} className="login-form">
            <label>
              Username
              <input value={username} onChange={(e) => setUsername(e.target.value)}
                autoComplete="username" autoFocus placeholder="e.g. admin" />
            </label>
            <label>
              Password
              <input type="password" value={password} onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password" placeholder="Your password" />
            </label>
            {err && <div className="login-err" role="alert">{err}</div>}
            <button className="primary login-submit" disabled={busy || !username || !password}>
              {busy ? "Signing in…" : "Sign in"}
            </button>
          </form>
        )}

        {config.local && config.entra && <div className="login-or"><span>or</span></div>}

        {config.entra && (
          <button className="entra-btn" onClick={entraSignIn} type="button">
            <span className="ms-logo" aria-hidden="true" />
            Sign in with Microsoft
          </button>
        )}

        {!config.local && !config.entra && (
          <p className="muted">No sign-in method is configured on this deployment.</p>
        )}

        <p className="login-foot">Roles control what you can do: Dispatcher · Warehouse Manager · Admin</p>
      </div>
    </div>
  );
}
