import { useEffect, useState } from 'react'
import { DatabaseBackup } from 'lucide-react'
import { Link } from 'react-router-dom'
import { configBackupsApi } from '../../api/configBackup'
import type { ConfigBackupListItem } from '../../types/api'
import { formatLocalDateTime } from '../../utils/format'

export function DeviceConfigSummary({ deviceId, ipAddress }: { deviceId: number; ipAddress: string }) {
  const [backups, setBackups] = useState<ConfigBackupListItem[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    const controller = new AbortController()
    configBackupsApi.list(deviceId, controller.signal).then(setBackups)
      .catch((loadError) => { if (!(loadError instanceof DOMException && loadError.name === 'AbortError')) setError(loadError instanceof Error ? loadError.message : 'Backup summary unavailable.') })
    return () => controller.abort()
  }, [deviceId])

  return <section className="panel device-intelligence-card">
    <header className="panel-header"><div><h2>Configuration</h2><p>Backup metadata only; running configuration is not loaded here.</p></div><DatabaseBackup size={19} aria-hidden="true" /></header>
    {error ? <div className="device-intelligence-error">{error}</div> : backups === null ? <div className="device-intelligence-empty">Loading backup summary…</div> : <div className="config-summary-content">
      <div><span>Latest backup</span><strong>{backups[0] ? formatLocalDateTime(backups[0].capturedAt) : 'No backups yet'}</strong></div>
      <div><span>Total backups</span><strong>{backups.length}</strong></div>
      <div><span>Latest ID</span><strong>{backups[0] ? `#${backups[0].id}` : '—'}</strong></div>
    </div>}
    <footer className="device-intelligence-actions"><Link className="button secondary compact-button" to={`/tools/config-backup?ip=${encodeURIComponent(ipAddress)}&deviceId=${deviceId}`}>Backup Now</Link><Link className="button secondary compact-button" to={`/tools/config-backup/history?deviceId=${deviceId}`}>View History</Link></footer>
  </section>
}
