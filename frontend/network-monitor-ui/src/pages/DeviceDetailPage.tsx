import { useCallback, useEffect, useMemo, useRef } from 'react'
import {
  Activity,
  ArrowDownToLine,
  ArrowLeft,
  ArrowUpToLine,
  Cable,
  BellRing,
  Clock3,
  DatabaseBackup,
  Gauge,
  ListChecks,
  Network,
  Power,
  XCircle,
} from 'lucide-react'
import { createSearchParams, Link, useParams } from 'react-router-dom'
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { devicesApi } from '../api/devices'
import { incidentsApi } from '../api/incidents'
import { DeviceConfigSummary } from '../components/device-detail/DeviceConfigSummary'
import { DevicePortSnapshot } from '../components/device-detail/DevicePortSnapshot'
import { DeviceSnmpPanel } from '../components/device-detail/DeviceSnmpPanel'
import { ConnectionIndicator } from '../components/realtime/ConnectionIndicator'
import { MetricCard } from '../components/ui/MetricCard'
import { StatePanel } from '../components/ui/StatePanel'
import { StatusBadge } from '../components/ui/StatusBadge'
import { useRealtimeResource } from '../hooks/useRealtimeResource'
import { useMonitoringUpdates } from '../realtime/useRealtime'
import type {
  CheckResult,
  Device,
  DeviceMonitoringUpdate,
  DeviceSummary,
  Incident,
} from '../types/api'
import {
  formatLatency,
  formatDuration,
  formatLocalDateTime,
  formatPercentage,
  formatRelativeTime,
  formatTime,
} from '../utils/format'

interface DetailData {
  device: Device
  summary: DeviceSummary
  checks: CheckResult[]
  incidents: Incident[]
}

interface ChartPoint {
  checkedAt: string
  timeLabel: string
  latencyMs: number | null
}

export function DeviceDetailPage() {
  const { id } = useParams()
  const deviceId = Number(id)
  const isValidId = Number.isInteger(deviceId) && deviceId > 0

  const loadDetail = useCallback(async (signal: AbortSignal): Promise<DetailData> => {
    if (!isValidId) throw new Error('Geçersiz cihaz kimliği.')
    const [device, summary, checks, incidents] = await Promise.all([
      devicesApi.get(deviceId, signal),
      devicesApi.summary(deviceId, signal),
      devicesApi.checks(deviceId, 100, signal),
      incidentsApi.byDevice(deviceId, signal),
    ])
    return { device, summary, checks, incidents }
  }, [deviceId, isValidId])

  const {
    data,
    setData,
    error,
    isLoading,
    isRefreshing,
    refresh,
  } = useRealtimeResource(loadDetail)
  const refreshTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const scheduleHistoryRefresh = useCallback(() => {
    if (refreshTimerRef.current) clearTimeout(refreshTimerRef.current)
    refreshTimerRef.current = setTimeout(() => {
      refreshTimerRef.current = null
      refresh()
    }, 250)
  }, [refresh])

  const applyMonitoringUpdate = useCallback((update: DeviceMonitoringUpdate) => {
    if (update.deviceId !== deviceId) return

    setData((currentData) =>
      currentData
        ? {
            ...currentData,
            device: {
              ...currentData.device,
              status: update.status,
              lastCheckedAt: update.lastCheckedAt,
              lastSeenAt: update.lastSeenAt,
              lastLatencyMs: update.lastLatencyMs,
              isMonitoringEnabled: update.isMonitoringEnabled,
            },
          }
        : currentData,
    )
    scheduleHistoryRefresh()
  }, [deviceId, scheduleHistoryRefresh, setData])
  useMonitoringUpdates(applyMonitoringUpdate)

  useEffect(() => () => {
    if (refreshTimerRef.current) clearTimeout(refreshTimerRef.current)
  }, [])

  const chartData = useMemo<ChartPoint[]>(
    () => (data?.checks ?? [])
      .slice()
      .reverse()
      .map((check) => ({
        checkedAt: check.checkedAt,
        timeLabel: formatTime(check.checkedAt),
        latencyMs: check.isSuccess ? check.latencyMs : null,
      })),
    [data?.checks],
  )

  if (isLoading && !data) {
    return <div className="page"><StatePanel type="loading" title="Loading device" message="Collecting device details and monitoring history…" /></div>
  }

  if (error && !data) {
    return (
      <div className="page">
        <Link className="back-link" to="/devices"><ArrowLeft size={16} /> Back to devices</Link>
        <StatePanel
          type="error"
          title="Device details unavailable"
          message={error}
          action={<button className="button secondary" type="button" onClick={refresh}>Try again</button>}
        />
      </div>
    )
  }

  if (!data) return null

  const { device, summary, checks, incidents } = data

  return (
    <div className="page">
      <Link className="back-link" to="/devices"><ArrowLeft size={16} /> Back to devices</Link>
      <header className="page-header detail-heading">
        <div>
          <span className="eyebrow">Device #{device.id}</span>
          <div className="title-with-status">
            <h1>{device.name}</h1>
            <StatusBadge status={device.status} />
          </div>
          <p className="mono">{device.ipAddress}</p>
        </div>
        <ConnectionIndicator compact isSyncing={isRefreshing} />
      </header>

      {error && <div className="inline-alert" role="alert">{error} Showing the last successful result.</div>}

      <section className="device-info-grid">
        <div><span>Description</span><strong>{device.description || 'No description'}</strong></div>
        <div><span>Monitoring</span><strong>{device.isMonitoringEnabled ? 'Enabled' : 'Paused'}</strong></div>
        <div><span>Last checked</span><strong title={formatLocalDateTime(device.lastCheckedAt)}>{formatRelativeTime(device.lastCheckedAt)}</strong></div>
        <div><span>Last seen</span><strong title={formatLocalDateTime(device.lastSeenAt)}>{formatRelativeTime(device.lastSeenAt)}</strong></div>
        <div><span>Current latency</span><strong>{formatLatency(device.lastLatencyMs)}</strong></div>
      </section>

      <section className="panel device-health-panel">
        <header className="panel-header"><div><h2>Device health</h2><p>Current state and reliable 24-hour monitoring summary.</p></div><StatusBadge status={device.status} /></header>
        <div className="device-health-summary"><div><span>24h success rate</span><strong>{formatPercentage(summary.uptimePercentage)}</strong></div><div><span>Last latency</span><strong>{formatLatency(device.lastLatencyMs)}</strong></div><div><span>Monitoring</span><strong>{device.isMonitoringEnabled ? 'Enabled' : 'Disabled'}</strong></div></div>
      </section>

      <section className="panel device-tools-panel">
        <header className="panel-header">
          <div>
            <h2>Quick actions</h2>
            <p>Open a network tool with this device selected. No scan or query starts automatically.</p>
          </div>
          <span className="device-tools-header-action"><Link className="button secondary compact-button" to={`/tools/config-backup/history?deviceId=${device.id}`}>Backup History</Link><Network size={19} aria-hidden="true" /></span>
        </header>
        <div className="device-tools-grid">
          <Link
            className="device-tool-action"
            to={`/tools/port-scanner?${createSearchParams({ ip: device.ipAddress })}`}
          >
            <span className="device-tool-icon"><Cable size={18} aria-hidden="true" /></span>
            <span><strong>Scan Ports</strong><small>Check selected TCP ports</small></span>
          </Link>
          <Link
            className="device-tool-action"
            to={`/tools/snmp?${createSearchParams({ ip: device.ipAddress })}`}
          >
            <span className="device-tool-icon"><Network size={18} aria-hidden="true" /></span>
            <span><strong>SNMP Explorer</strong><small>Inspect SNMP data</small></span>
          </Link>
          <Link className="device-tool-action" to="/tools/wake-on-lan">
            <span className="device-tool-icon"><Power size={18} aria-hidden="true" /></span>
            <span><strong>Wake-on-LAN</strong><small>Enter MAC and broadcast details</small></span>
          </Link>
          <Link className="device-tool-action" to="/topology">
            <span className="device-tool-icon"><Network size={18} aria-hidden="true" /></span>
            <span><strong>Topology</strong><small>Open LLDP topology discovery</small></span>
          </Link>
          <Link
            className="device-tool-action"
            to={`/tools/config-backup?${createSearchParams({ ip: device.ipAddress, deviceId: String(device.id) })}`}
          >
            <span className="device-tool-icon"><DatabaseBackup size={18} aria-hidden="true" /></span>
            <span><strong>Config Backup</strong><small>Retrieve running configuration</small></span>
          </Link>
        </div>
      </section>

      <section className="panel device-incidents-panel">
        <header className="panel-header">
          <div><h2>Incidents</h2><p>{incidents.filter((incident) => incident.status === 'Open').length} active · latest confirmed monitoring outages.</p></div>
          <span className="device-tools-header-action"><Link className="button secondary compact-button" to="/incidents">All incidents</Link><BellRing size={19} aria-hidden="true" /></span>
        </header>
        {incidents.length === 0 ? (
          <div className="device-incident-empty">No incidents recorded for this device.</div>
        ) : (
          <div className="device-incident-list">{incidents.slice(0, 5).map((incident) => (
            <div key={incident.id} className={`device-incident ${incident.status === 'Open' ? 'open' : ''}`}>
              <span className={`incident-status ${incident.status.toLowerCase()}`}>{incident.status}</span>
              <div><strong>{incident.summary}</strong><small>{incident.status === 'Open' ? `Started ${formatRelativeTime(incident.startedAt)}` : `Resolved · ${formatDuration(incident.durationSeconds)}`}</small></div>
            </div>
          ))}</div>
        )}
      </section>

      <section className="device-intelligence-grid">
        <DeviceConfigSummary deviceId={device.id} ipAddress={device.ipAddress} />
        <DevicePortSnapshot ipAddress={device.ipAddress} />
      </section>
      <DeviceSnmpPanel ipAddress={device.ipAddress} />

      <section className="metrics-grid detail-metrics" aria-label="24 hour monitoring summary">
        <MetricCard label="24h uptime" value={formatPercentage(summary.uptimePercentage)} hint={`${summary.successfulChecks} successful checks`} icon={Activity} tone="up" />
        <MetricCard label="Average latency" value={formatLatency(summary.averageLatencyMs)} hint="Successful checks" icon={Gauge} />
        <MetricCard label="Minimum latency" value={formatLatency(summary.minLatencyMs)} hint="Fastest response" icon={ArrowDownToLine} />
        <MetricCard label="Maximum latency" value={formatLatency(summary.maxLatencyMs)} hint="Slowest response" icon={ArrowUpToLine} />
        <MetricCard label="Total checks" value={summary.totalChecks} hint="Last 24 hours" icon={ListChecks} />
        <MetricCard label="Failed" value={summary.failedChecks} hint="Last 24 hours" icon={XCircle} tone="down" />
      </section>

      <section className="panel chart-panel">
        <header className="panel-header">
          <div>
            <h2>Latency history</h2>
            <p>Last {Math.min(checks.length, 100)} checks. Gaps indicate failed checks.</p>
          </div>
          <span className="latency-range">{formatLatency(summary.minLatencyMs)} — {formatLatency(summary.maxLatencyMs)}</span>
        </header>
        {chartData.length === 0 ? (
          <StatePanel type="empty" title="No check history" message="Latency data will appear after the monitoring engine completes its first check." />
        ) : (
          <div className="chart-container" aria-label="Latency history chart">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={chartData} margin={{ top: 12, right: 20, left: -12, bottom: 0 }}>
                <CartesianGrid strokeDasharray="4 4" vertical={false} stroke="rgba(148, 163, 184, 0.14)" />
                <XAxis dataKey="timeLabel" tick={{ fill: '#738297', fontSize: 11 }} axisLine={false} tickLine={false} minTickGap={32} />
                <YAxis unit=" ms" tick={{ fill: '#738297', fontSize: 11 }} axisLine={false} tickLine={false} width={62} />
                <Tooltip
                  contentStyle={{ background: '#111a27', border: '1px solid #27364b', borderRadius: 10 }}
                  labelStyle={{ color: '#e6edf7' }}
                  formatter={(value) => [`${String(value)} ms`, 'Latency']}
                  labelFormatter={(_, payload) => {
                    const point = payload[0]?.payload as ChartPoint | undefined
                    return point ? formatLocalDateTime(point.checkedAt) : ''
                  }}
                />
                <Line type="monotone" dataKey="latencyMs" name="Latency" stroke="#30c7d7" strokeWidth={2.5} dot={false} activeDot={{ r: 4, fill: '#30c7d7' }} connectNulls={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </section>

      <section className="panel">
        <header className="panel-header">
          <div><h2>Recent checks</h2><p>Newest results from the persistent monitoring history.</p></div>
          <Clock3 size={19} aria-hidden="true" />
        </header>
        {checks.length === 0 ? (
          <StatePanel type="empty" title="No checks recorded" message="Keep monitoring enabled to collect history." />
        ) : (
          <div className="table-scroll">
            <table className="data-table compact-table">
              <thead><tr><th>Checked at</th><th>Result</th><th>Status</th><th>Latency</th><th>Failure reason</th></tr></thead>
              <tbody>
                {checks.map((check) => (
                  <tr key={check.id}>
                    <td>{formatLocalDateTime(check.checkedAt)}</td>
                    <td><span className={check.isSuccess ? 'check-success' : 'check-failed'}>{check.isSuccess ? 'Success' : 'Failed'}</span></td>
                    <td><StatusBadge status={check.deviceStatus} /></td>
                    <td className="mono">{formatLatency(check.latencyMs)}</td>
                    <td>{check.failureReason || '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}
