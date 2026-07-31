import { useEffect, useState } from 'react'
import { LoaderCircle, Mail, Save, Send, ShieldCheck } from 'lucide-react'
import { emailNotificationsApi } from '../api/emailNotifications'
import { StatePanel } from '../components/ui/StatePanel'
import type { EmailNotificationSettings, EmailTlsMode } from '../types/api'

export function EmailNotificationSettingsPage() {
  const [settings, setSettings] = useState<EmailNotificationSettings | null>(null)
  const [isEnabled, setIsEnabled] = useState(false)
  const [host, setHost] = useState('')
  const [port, setPort] = useState('587')
  const [tlsMode, setTlsMode] = useState<EmailTlsMode>('StartTls')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [fromAddress, setFromAddress] = useState('')
  const [fromName, setFromName] = useState('NetScope')
  const [recipients, setRecipients] = useState('')
  const [busy, setBusy] = useState<'save' | 'test' | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const apply = (value: EmailNotificationSettings) => {
    setSettings(value); setIsEnabled(value.isEnabled); setHost(value.host); setPort(String(value.port)); setTlsMode(value.tlsMode)
    setUsername(value.username ?? ''); setPassword(''); setFromAddress(value.fromAddress); setFromName(value.fromName ?? '')
    setRecipients(value.recipientAddresses.join('\n'))
  }
  const load = (signal?: AbortSignal) => emailNotificationsApi.get(signal).then(apply).catch((loadError) => {
    if (!(loadError instanceof DOMException && loadError.name === 'AbortError')) setError(loadError instanceof Error ? loadError.message : 'Email notification settings are unavailable.')
  })
  useEffect(() => { const controller = new AbortController(); void load(controller.signal); return () => controller.abort() }, [])

  const normalizedRecipients = () => [...new Set(recipients.split(/[\n,;]+/).map((value) => value.trim()).filter(Boolean))]
  const validate = () => {
    const parsedPort = Number(port)
    if (!Number.isInteger(parsedPort) || parsedPort < 1 || parsedPort > 65535) return 'SMTP port must be between 1 and 65535.'
    const invalidRecipient = normalizedRecipients().find((address) => !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(address))
    if (invalidRecipient) return `Invalid recipient address: ${invalidRecipient}`
    if (isEnabled && !host.trim()) return 'SMTP host is required when email notifications are enabled.'
    if (isEnabled && !fromAddress.trim()) return 'From address is required when email notifications are enabled.'
    if (isEnabled && normalizedRecipients().length === 0) return 'Add at least one recipient before enabling email notifications.'
    if (username.trim() && !password && !settings?.hasPassword) return 'SMTP password is required for the configured username.'
    return null
  }
  const save = async (event: React.FormEvent) => {
    event.preventDefault(); setError(null); setSuccess(null)
    const validationError = validate(); if (validationError) { setError(validationError); return }
    setBusy('save')
    try {
      const updated = await emailNotificationsApi.update({
        isEnabled, host: host.trim(), port: Number(port), tlsMode, username: username.trim() || null,
        password: password || null, fromAddress: fromAddress.trim(), fromName: fromName.trim() || null,
        recipientAddresses: normalizedRecipients(),
      })
      apply(updated); setSuccess('Email notification settings saved.')
    } catch (saveError) { setPassword(''); setError(saveError instanceof Error ? saveError.message : 'Email notification settings could not be saved.') }
    finally { setBusy(null) }
  }
  const sendTest = async () => {
    setBusy('test'); setError(null); setSuccess(null)
    try { const response = await emailNotificationsApi.sendTest(); setSuccess(response.message) }
    catch (sendError) { setError(sendError instanceof Error ? sendError.message : 'Test email could not be sent.') }
    finally { setBusy(null) }
  }

  if (!settings && !error) return <div className="page"><StatePanel type="loading" title="Loading email settings" message="Reading SMTP configuration…" /></div>
  if (!settings) return <div className="page"><StatePanel type="error" title="Email settings unavailable" message={error ?? 'Settings could not be loaded.'} action={<button className="button secondary" type="button" onClick={() => { setError(null); void load() }}>Try again</button>} /></div>

  return <div className="page">
    <header className="page-header"><div><span className="eyebrow">Notification settings</span><h1>Email Notifications</h1><p>Deliver new incident notifications through one deployment-wide SMTP channel.</p></div><span className={`email-channel-state ${isEnabled ? 'enabled' : ''}`}><span />{isEnabled ? 'Enabled' : 'Disabled'}</span></header>
    {error && <div className="form-error">{error}</div>}{success && <div className="form-success">{success}</div>}
    <form className="panel email-settings-panel" onSubmit={save}>
      <header className="panel-header"><div><h2>SMTP channel</h2><p>Settings and recipients are shared by every authenticated user in this deployment.</p></div><Mail size={20} /></header>
      <label className="email-enabled-toggle"><input type="checkbox" checked={isEnabled} onChange={(event) => setIsEnabled(event.target.checked)} /><span><strong>Enable Email Notifications</strong><small>Only notifications created after enabling will be scheduled for email.</small></span></label>
      <div className="email-settings-grid">
        <label>SMTP Host<input value={host} onChange={(event) => setHost(event.target.value)} placeholder="smtp.example.com" maxLength={255} /></label>
        <label>Port<input type="number" min="1" max="65535" value={port} onChange={(event) => setPort(event.target.value)} /></label>
        <label>TLS Mode<select value={tlsMode} onChange={(event) => setTlsMode(event.target.value as EmailTlsMode)}><option value="StartTls">STARTTLS</option><option value="SslOnConnect">SSL/TLS on connect</option><option value="None">None (not recommended)</option></select></label>
        <label>Username<input value={username} onChange={(event) => setUsername(event.target.value)} autoComplete="username" maxLength={255} placeholder="Optional" /></label>
        <label>Password<input type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="new-password" maxLength={1024} placeholder={settings.hasPassword ? 'Configured — leave blank to keep' : 'Not configured'} /><small>{settings.hasPassword ? 'A protected password is configured.' : 'No SMTP password is configured.'}</small></label>
        <label>From Address<input type="email" value={fromAddress} onChange={(event) => setFromAddress(event.target.value)} placeholder="netscope@example.com" maxLength={320} /></label>
        <label>From Name<input value={fromName} onChange={(event) => setFromName(event.target.value)} placeholder="NetScope" maxLength={100} /></label>
        <label className="email-recipient-field">Recipients<textarea value={recipients} onChange={(event) => setRecipients(event.target.value)} placeholder={'alerts@example.com\nadmin@example.com'} rows={4} /><small>One per line, or separate addresses with commas.</small></label>
      </div>
      <div className="email-security-note"><ShieldCheck size={17} /><span>The SMTP password is encrypted with the existing ASP.NET Core Data Protection key ring. It is never returned to this browser.</span></div>
      <div className="email-settings-actions"><button className="button primary" type="submit" disabled={busy !== null}>{busy === 'save' ? <LoaderCircle className="spin" size={15} /> : <Save size={15} />}Save settings</button><button className="button secondary" type="button" disabled={busy !== null || !settings.updatedAt} onClick={() => void sendTest()}>{busy === 'test' ? <LoaderCircle className="spin" size={15} /> : <Send size={15} />}Send Test Email</button><small>Test email uses the last saved settings and only confirms SMTP acceptance.</small></div>
    </form>
  </div>
}
