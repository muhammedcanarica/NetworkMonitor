import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { AlertTriangle, CheckCircle2, Clock3, Timer } from 'lucide-react'
import { Link } from 'react-router-dom'
import { incidentsApi } from '../api/incidents'
import { MetricCard } from '../components/ui/MetricCard'
import { StatePanel } from '../components/ui/StatePanel'
import { useRealtimeResource } from '../hooks/useRealtimeResource'
import { useMonitoringUpdates } from '../realtime/useRealtime'
import type { Incident, IncidentStatus } from '../types/api'
import { formatDuration, formatLocalDateTime } from '../utils/format'

type Filter = 'All' | IncidentStatus

export function IncidentsPage() {
  const [filter, setFilter] = useState<Filter>('All')
  const loadIncidents = useCallback((signal: AbortSignal) => incidentsApi.list(undefined, signal), [])
  const { data: incidents, error, isLoading, refresh } = useRealtimeResource(loadIncidents)
  const refreshTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const scheduleRefresh = useCallback(() => {
    if (refreshTimer.current) clearTimeout(refreshTimer.current)
    refreshTimer.current = setTimeout(() => { refreshTimer.current = null; refresh() }, 250)
  }, [refresh])
  useMonitoringUpdates(scheduleRefresh)
  useEffect(() => () => { if (refreshTimer.current) clearTimeout(refreshTimer.current) }, [])
  const visible = useMemo(() => incidents?.filter((incident) => filter === 'All' || incident.status === filter) ?? [], [filter, incidents])
  const metrics = useMemo(() => calculateMetrics(incidents ?? []), [incidents])

  return (
    <div className="page">
      <header className="page-header">
        <div><span className="eyebrow">Monitoring history</span><h1>Incidents</h1><p>Persistent incidents are opened and resolved only by confirmed monitoring status transitions.</p></div>
      </header>
      <section className="metrics-grid" aria-label="Incident summary">
        <MetricCard label="Active incidents" value={metrics.open} hint="Currently unresolved" icon={AlertTriangle} tone={metrics.open > 0 ? 'down' : 'up'} />
        <MetricCard label="Resolved today" value={metrics.resolvedToday} hint="Completed in local day" icon={CheckCircle2} tone="up" />
        <MetricCard label="Average resolution" value={metrics.averageResolution === null ? '—' : formatDuration(metrics.averageResolution)} hint="Resolved incidents" icon={Timer} />
      </section>
      <section className="panel">
        <header className="panel-header"><div><h2>Incident timeline</h2><p>Newest 200 incidents. Open incidents use the current time for duration.</p></div><Clock3 size={19} aria-hidden="true" /></header>
        <div className="port-result-filters" role="group" aria-label="Incident filters">
          {(['All', 'Open', 'Resolved'] as Filter[]).map((value) => <button key={value} type="button" className={filter === value ? 'active' : ''} onClick={() => setFilter(value)}>{value}</button>)}
        </div>
        {isLoading && !incidents ? <StatePanel type="loading" title="Loading incidents" message="Reading persistent incident history…" />
          : error && !incidents ? <StatePanel type="error" title="Incidents unavailable" message={error} action={<button className="button secondary" type="button" onClick={refresh}>Try again</button>} />
            : visible.length === 0 ? <StatePanel type="empty" title={filter === 'Open' ? 'No active incidents' : 'No incidents recorded'} message={filter === 'Open' ? 'All monitored devices are currently clear.' : 'Confirmed monitoring outages will appear here.'} />
              : <div className="table-scroll"><table className="data-table incidents-table"><thead><tr><th>Device</th><th>Status</th><th>Type</th><th>Started</th><th>Resolved</th><th>Duration</th></tr></thead><tbody>{visible.map((incident) => <IncidentRow key={incident.id} incident={incident} />)}</tbody></table></div>}
      </section>
    </div>
  )
}

function IncidentRow({ incident }: { incident: Incident }) {
  return <tr className={incident.status === 'Open' ? 'incident-open-row' : ''}>
    <td><Link className="incident-device-link" to={`/devices/${incident.deviceId}`}>{incident.deviceName}<small>{incident.deviceIpAddress}</small></Link></td>
    <td><span className={`incident-status ${incident.status.toLowerCase()}`}>{incident.status}</span></td>
    <td>{incident.summary}</td><td>{formatLocalDateTime(incident.startedAt)}</td><td>{incident.resolvedAt ? formatLocalDateTime(incident.resolvedAt) : '—'}</td><td className="mono">{formatDuration(incident.durationSeconds)}</td>
  </tr>
}

function calculateMetrics(incidents: Incident[]) {
  const today = new Date().toDateString()
  const resolved = incidents.filter((item) => item.status === 'Resolved')
  return {
    open: incidents.filter((item) => item.status === 'Open').length,
    resolvedToday: resolved.filter((item) => item.resolvedAt && new Date(item.resolvedAt).toDateString() === today).length,
    averageResolution: resolved.length === 0 ? null : Math.round(resolved.reduce((total, item) => total + item.durationSeconds, 0) / resolved.length),
  }
}
