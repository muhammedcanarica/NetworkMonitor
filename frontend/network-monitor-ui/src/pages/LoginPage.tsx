import { useState } from 'react'
import { Activity, LoaderCircle, LockKeyhole } from 'lucide-react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function LoginPage() {
  const { user, login } = useAuth(); const navigate = useNavigate(); const location = useLocation(); const [username, setUsername] = useState(''); const [password, setPassword] = useState(''); const [busy, setBusy] = useState(false); const [error, setError] = useState<string | null>(null)
  if (user) return <Navigate to="/" replace />
  const submit = async (event: React.FormEvent) => { event.preventDefault(); setBusy(true); setError(null); try { await login(username, password); navigate((location.state as { from?: string } | null)?.from ?? '/', { replace: true }) } catch { setError('Invalid username or password.') } finally { setBusy(false); setPassword('') } }
  return <main className="login-page"><section className="login-card"><div className="login-brand"><Activity size={26} /><div><strong>NetScope</strong><span>Secure network operations</span></div></div><form onSubmit={submit}><h1>Sign in</h1><p>Use the local administrator account configured for this deployment.</p>{error && <div className="form-error">{error}</div>}<label>Username<input value={username} onChange={(event) => setUsername(event.target.value)} autoComplete="username" /></label><label>Password<input type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="current-password" /></label><button className="button primary" disabled={busy}>{busy ? <LoaderCircle className="spin" size={16} /> : <LockKeyhole size={16} />}{busy ? 'Signing in…' : 'Sign in'}</button></form></section></main>
}
