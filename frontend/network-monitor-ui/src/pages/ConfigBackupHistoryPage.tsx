import { useEffect, useMemo, useRef, useState } from 'react'
import { ArrowLeft, GitCompareArrows, LoaderCircle, ShieldCheck } from 'lucide-react'
import { Link, useSearchParams } from 'react-router-dom'
import { configBackupsApi } from '../api/configBackup'
import { StatePanel } from '../components/ui/StatePanel'
import type { ConfigBackupComparison, ConfigBackupListItem } from '../types/api'

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Backup history could not be loaded.'
}

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === 'AbortError'
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

export function ConfigBackupHistoryPage() {
  const [searchParams] = useSearchParams()
  const deviceIdValue = Number(searchParams.get('deviceId'))
  const deviceId = Number.isInteger(deviceIdValue) && deviceIdValue > 0 ? deviceIdValue : undefined
  const [backups, setBackups] = useState<ConfigBackupListItem[]>([])
  const [fromId, setFromId] = useState<number | null>(null)
  const [toId, setToId] = useState<number | null>(null)
  const [comparison, setComparison] = useState<ConfigBackupComparison | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isComparing, setIsComparing] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const requestController = useRef<AbortController | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    requestController.current = controller
    setIsLoading(true)
    setError(null)
    setComparison(null)
    configBackupsApi.list(deviceId, controller.signal)
      .then((items) => {
        setBackups(items)
        setToId(items[0]?.id ?? null)
        setFromId(items[1]?.id ?? null)
      })
      .catch((loadError: unknown) => {
        if (!isAbortError(loadError)) setError(getErrorMessage(loadError))
      })
      .finally(() => setIsLoading(false))

    return () => controller.abort()
  }, [deviceId])

  const selectedBackups = useMemo(() => ({
    from: backups.find((backup) => backup.id === fromId),
    to: backups.find((backup) => backup.id === toId),
  }), [backups, fromId, toId])

  const compare = async () => {
    if (!fromId || !toId || fromId === toId) {
      setError('Choose two different backups to compare.')
      return
    }

    const controller = new AbortController()
    requestController.current = controller
    setIsComparing(true)
    setError(null)
    try {
      setComparison(await configBackupsApi.compare(fromId, toId, controller.signal))
    } catch (compareError) {
      if (!isAbortError(compareError)) setError(getErrorMessage(compareError))
    } finally {
      if (requestController.current === controller) requestController.current = null
      setIsComparing(false)
    }
  }

  const historyLabel = deviceId ? `Device #${deviceId} backup history` : 'Configuration backup history'

  return (
    <div className="page">
      <Link className="back-link" to="/tools/config-backup"><ArrowLeft size={16} /> Back to Configuration Backup</Link>
      <header className="page-header">
        <div>
          <span className="eyebrow">Saved configuration snapshots</span>
          <h1>Configuration History</h1>
          <p>{historyLabel}. Configurations are loaded only when saved, compared, or opened in detail.</p>
        </div>
      </header>

      <div className="inline-alert config-sensitive-note" role="status"><ShieldCheck size={16} /> Saved configurations may contain sensitive network information.</div>

      {error && <section className="panel"><StatePanel type="error" title="Backup history unavailable" message={error} /></section>}

      {isLoading ? (
        <section className="panel"><StatePanel type="loading" title="Loading backup history" message="Retrieving saved backup metadata…" /></section>
      ) : backups.length === 0 ? (
        <section className="panel"><StatePanel type="empty" title="No saved backups" message="Retrieve a configuration, then choose Save Backup to build history." /></section>
      ) : (
        <>
          <section className="panel">
            <header className="panel-header">
              <div><h2>Saved backups</h2><p>Configuration content is excluded from this list.</p></div>
              <span className="record-count">{backups.length} backups</span>
            </header>
            <div className="table-scroll">
              <table className="data-table config-history-table">
                <thead><tr><th>Device / IP</th><th>Vendor</th><th>Captured</th><th>Created</th><th>Size</th><th>Hash</th></tr></thead>
                <tbody>{backups.map((backup) => (
                  <tr key={backup.id}>
                    <td>{backup.deviceId ? <Link className="device-name" to={`/devices/${backup.deviceId}`}>Device #{backup.deviceId}<small>{backup.ipAddress}</small></Link> : <span className="mono">{backup.ipAddress}</span>}</td>
                    <td>Cisco IOS / IOS-XE</td><td>{formatDate(backup.capturedAt)}</td><td>{formatDate(backup.createdAt)}</td><td className="mono">{backup.configurationLength.toLocaleString()} chars</td><td className="mono" title={backup.hash}>{backup.hash.slice(0, 12)}…</td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          </section>

          <section className="panel config-compare-panel">
            <header className="panel-header"><div><h2>Compare backups</h2><p>Select a before and after snapshot to produce a line-based diff.</p></div><GitCompareArrows size={20} aria-hidden="true" /></header>
            <div className="config-compare-form">
              <label>Before<select value={fromId ?? ''} onChange={(event) => setFromId(Number(event.target.value) || null)} disabled={isComparing}><option value="">Select backup</option>{backups.map((backup) => <option key={backup.id} value={backup.id}>{formatDate(backup.capturedAt)} · {backup.ipAddress}</option>)}</select></label>
              <label>After<select value={toId ?? ''} onChange={(event) => setToId(Number(event.target.value) || null)} disabled={isComparing}><option value="">Select backup</option>{backups.map((backup) => <option key={backup.id} value={backup.id}>{formatDate(backup.capturedAt)} · {backup.ipAddress}</option>)}</select></label>
              <button className="button primary" type="button" onClick={compare} disabled={isComparing || !selectedBackups.from || !selectedBackups.to || fromId === toId}>{isComparing ? <LoaderCircle className="spin" size={16} /> : <GitCompareArrows size={16} />}{isComparing ? 'Comparing…' : 'Compare'}</button>
            </div>
          </section>

          {comparison && (
            <section className="panel config-diff-panel">
              <header className="panel-header"><div><h2>Configuration diff</h2><p>{comparison.changed ? 'Line-based comparison of the selected backups.' : 'No configuration changes detected.'}</p></div><span className="diff-summary"><strong>+ {comparison.addedLines}</strong> added <strong>− {comparison.removedLines}</strong> removed</span></header>
              <div className="config-diff-viewer">{comparison.diffLines.map((line, index) => <div className={`config-diff-line ${line.type.toLowerCase()}`} key={`${line.type}-${line.fromLineNumber}-${line.toLineNumber}-${index}`}><span>{line.type === 'Added' ? '+' : line.type === 'Removed' ? '−' : ' '}</span><code>{line.content}</code></div>)}</div>
            </section>
          )}
        </>
      )}
    </div>
  )
}
