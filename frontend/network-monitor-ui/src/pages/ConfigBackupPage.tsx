import { useEffect, useRef, useState } from 'react'
import { DatabaseBackup, Download, FileText, LoaderCircle, ShieldCheck, X } from 'lucide-react'
import { Link, useSearchParams } from 'react-router-dom'
import { configBackupApi, configBackupsApi } from '../api/configBackup'
import { StatePanel } from '../components/ui/StatePanel'
import type {
  ConfigBackupRequest,
  ConfigBackupResponse,
  SaveConfigBackupResponse,
} from '../types/api'

interface ConfigBackupForm {
  ipAddress: string
  port: string
  username: string
  password: string
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Configuration backup could not be completed.'
}

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === 'AbortError'
}

function isValidIpv4Address(value: string) {
  const segments = value.split('.')
  return segments.length === 4 && segments.every((segment) => {
    if (!/^\d{1,3}$/.test(segment)) return false
    const number = Number(segment)
    return number >= 0 && number <= 255
  })
}

function downloadConfiguration(result: ConfigBackupResponse) {
  const blob = new Blob([result.configuration], { type: 'text/plain;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = result.suggestedFileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

export function ConfigBackupPage() {
  const [searchParams] = useSearchParams()
  const [form, setForm] = useState<ConfigBackupForm>(() => ({
    ipAddress: searchParams.get('ip')?.trim() ?? '',
    port: '22',
    username: '',
    password: '',
  }))
  const [result, setResult] = useState<ConfigBackupResponse | null>(null)
  const [savedBackup, setSavedBackup] = useState<SaveConfigBackupResponse | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const requestController = useRef<AbortController | null>(null)
  const saveController = useRef<AbortController | null>(null)
  const deviceIdValue = Number(searchParams.get('deviceId'))
  const deviceId = Number.isInteger(deviceIdValue) && deviceIdValue > 0 ? deviceIdValue : null

  useEffect(() => () => {
    requestController.current?.abort()
    saveController.current?.abort()
  }, [])

  useEffect(() => {
    const queryIpAddress = searchParams.get('ip')?.trim()
    if (queryIpAddress) setForm((current) => ({ ...current, ipAddress: queryIpAddress }))
  }, [searchParams])

  const updateField = (field: keyof ConfigBackupForm, value: string) => {
    setForm((current) => ({ ...current, [field]: value }))
  }

  const createRequest = (): ConfigBackupRequest | null => {
    const ipAddress = form.ipAddress.trim()
    const port = Number(form.port)

    if (!isValidIpv4Address(ipAddress)) {
      setError('Enter a valid target IPv4 address.')
      return null
    }
    if (!Number.isInteger(port) || port < 1 || port > 65535) {
      setError('SSH port must be between 1 and 65535.')
      return null
    }
    if (!form.username.trim()) {
      setError('Username is required.')
      return null
    }
    if (!form.password) {
      setError('Password is required.')
      return null
    }

    return {
      ipAddress,
      port,
      username: form.username.trim(),
      password: form.password,
      vendor: 'CiscoIos',
    }
  }

  const getConfiguration = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setNotice(null)
    const request = createRequest()
    if (!request) return

    const controller = new AbortController()
    requestController.current = controller
    setIsLoading(true)
    setResult(null)
    setSavedBackup(null)

    try {
      setResult(await configBackupApi.getRunningConfiguration(request, controller.signal))
    } catch (requestError) {
      if (isAbortError(requestError)) setNotice('Configuration backup cancelled.')
      else setError(getErrorMessage(requestError))
    } finally {
      if (requestController.current === controller) requestController.current = null
      setForm((current) => ({ ...current, password: '' }))
      setIsLoading(false)
    }
  }

  const saveBackup = async () => {
    if (!result) return

    const controller = new AbortController()
    saveController.current = controller
    setIsSaving(true)
    setError(null)
    setNotice(null)

    try {
      const saved = await configBackupsApi.save({
        deviceId,
        ipAddress: result.ipAddress,
        vendor: result.vendor,
        configuration: result.configuration,
        capturedAt: result.capturedAt,
      }, controller.signal)
      setSavedBackup(saved)
      setNotice(saved.configurationChanged
        ? `Backup #${saved.backupId} saved.`
        : `No configuration changes detected. Existing backup #${saved.existingBackupId} is already stored.`)
    } catch (saveError) {
      if (isAbortError(saveError)) setNotice('Backup save cancelled.')
      else setError(getErrorMessage(saveError))
    } finally {
      if (saveController.current === controller) saveController.current = null
      setIsSaving(false)
    }
  }

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Read-only device backup</span>
          <h1>Config Backup</h1>
          <p>Retrieve a Cisco IOS / IOS-XE running configuration over SSH without changing the device.</p>
        </div>
        <Link className="button secondary" to={deviceId ? `/tools/config-backup/history?deviceId=${deviceId}` : '/tools/config-backup/history'}>Backup History</Link>
      </header>

      {notice && <div className="success-alert" role="status">{notice}</div>}

      <section className="panel config-backup-control-panel">
        <header className="panel-header">
          <div>
            <h2>SSH connection</h2>
            <p>Credentials are used only for this request and are never saved by NetScope.</p>
          </div>
          <DatabaseBackup size={22} aria-hidden="true" />
        </header>

        <form className="config-backup-form" onSubmit={getConfiguration}>
          <label>
            Target IPv4
            <input value={form.ipAddress} onChange={(event) => updateField('ipAddress', event.target.value)} placeholder="192.168.1.10" disabled={isLoading} spellCheck="false" autoComplete="off" />
          </label>
          <label>
            SSH port
            <input type="number" min={1} max={65535} step={1} value={form.port} onChange={(event) => updateField('port', event.target.value)} disabled={isLoading} />
          </label>
          <label>
            Vendor
            <select value="CiscoIos" disabled={isLoading}>
              <option value="CiscoIos">Cisco IOS / IOS-XE</option>
            </select>
          </label>
          <label>
            Username
            <input value={form.username} onChange={(event) => updateField('username', event.target.value)} disabled={isLoading} autoComplete="username" />
          </label>
          <label>
            Password
            <input type="password" value={form.password} onChange={(event) => updateField('password', event.target.value)} disabled={isLoading} autoComplete="current-password" />
          </label>
          <div className="config-backup-submit">
            <button className="button primary" type="submit" disabled={isLoading || isSaving}>
              {isLoading ? <LoaderCircle className="spin" size={16} /> : <DatabaseBackup size={16} />}
              {isLoading ? 'Retrieving…' : 'Get Configuration'}
            </button>
          </div>
        </form>

        <div className="config-backup-security-note">
          <ShieldCheck size={17} aria-hidden="true" />
          <span><strong>Read-only scope:</strong> NetScope sends only the Cisco <code>show running-config</code> command. Configuration is not written to the device or server filesystem, and is stored only when you explicitly choose Save Backup.</span>
        </div>
      </section>

      {error && <section className="panel"><StatePanel type="error" title="Configuration backup failed" message={error} /></section>}

      {isLoading && (
        <section className="panel">
          <StatePanel type="loading" title="Retrieving running configuration" message="Connecting to the device and reading its configuration…" action={<button className="button secondary" type="button" onClick={() => requestController.current?.abort()}><X size={15} /> Cancel</button>} />
        </section>
      )}

      {result && (
        <section className="panel config-backup-result-panel">
          <header className="panel-header">
            <div>
              <h2>Running configuration</h2>
              <p>{result.ipAddress} · Cisco IOS / IOS-XE · captured {new Date(result.capturedAt).toLocaleString()}</p>
            </div>
            <div className="config-result-actions">
              <button className="button secondary" type="button" onClick={() => downloadConfiguration(result)}><Download size={16} /> Download .txt</button>
              <button className="button primary" type="button" onClick={saveBackup} disabled={isSaving || savedBackup !== null}>
                {isSaving ? <LoaderCircle className="spin" size={16} /> : <DatabaseBackup size={16} />}
                {savedBackup ? 'Backup saved' : isSaving ? 'Saving…' : 'Save Backup'}
              </button>
            </div>
          </header>
          <div className="config-file-name"><FileText size={15} aria-hidden="true" /> {result.suggestedFileName}</div>
          <pre className="config-viewer"><code>{result.configuration}</code></pre>
        </section>
      )}

      {!result && !isLoading && !error && <section className="panel"><StatePanel type="empty" title="No configuration retrieved" message="Enter SSH connection details to retrieve a read-only running configuration." /></section>}
    </div>
  )
}
