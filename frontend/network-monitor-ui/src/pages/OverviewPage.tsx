import { useCallback, useMemo } from 'react'
import { Activity, AlertTriangle, BellRing, CircleX, Server } from 'lucide-react'
import { devicesApi } from '../api/devices'
import { incidentsApi } from '../api/incidents'
import { DeviceTable } from '../components/devices/DeviceTable'
import { ConnectionIndicator } from '../components/realtime/ConnectionIndicator'
import { MetricCard } from '../components/ui/MetricCard'
import { StatePanel } from '../components/ui/StatePanel'
import { useRealtimeResource } from '../hooks/useRealtimeResource'
import { useMonitoringUpdates } from '../realtime/useRealtime'
import type { DeviceMonitoringUpdate } from '../types/api'

export function OverviewPage() {
  const loadDevices = useCallback(
    (signal: AbortSignal) => devicesApi.list(signal),
    [],
  )
  const {
    data: devices,
    setData: setDevices,
    error,
    isLoading,
    isRefreshing,
    refresh,
  } = useRealtimeResource(loadDevices)
  const { data: openIncidents, refresh: refreshOpenIncidents } = useRealtimeResource(
    useCallback((signal: AbortSignal) => incidentsApi.list('Open', signal), []),
  )

  const applyMonitoringUpdate = useCallback((update: DeviceMonitoringUpdate) => {
    setDevices((currentDevices) =>
      currentDevices?.map((device) =>
        device.id === update.deviceId
          ? {
              ...device,
              status: update.status,
              lastCheckedAt: update.lastCheckedAt,
              lastSeenAt: update.lastSeenAt,
              lastLatencyMs: update.lastLatencyMs,
              isMonitoringEnabled: update.isMonitoringEnabled,
            }
          : device,
      ) ?? null,
    )
    refreshOpenIncidents()
  }, [refreshOpenIncidents, setDevices])
  useMonitoringUpdates(applyMonitoringUpdate)

  const counts = useMemo(() => ({
    total: devices?.length ?? 0,
    up: devices?.filter((device) => device.status === 'Up').length ?? 0,
    warning: devices?.filter((device) => device.status === 'Warning').length ?? 0,
    down: devices?.filter((device) => device.status === 'Down').length ?? 0,
  }), [devices])

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Network operations</span>
          <h1>Overview</h1>
          <p>Live health and latency status across monitored devices.</p>
        </div>
        <ConnectionIndicator compact isSyncing={isRefreshing} />
      </header>

      <section className="metrics-grid" aria-label="Device summary">
        <MetricCard label="Total devices" value={counts.total} hint="Registered inventory" icon={Server} />
        <MetricCard label="Up" value={counts.up} hint="Responding normally" icon={Activity} tone="up" />
        <MetricCard label="Warning" value={counts.warning} hint="Needs attention" icon={AlertTriangle} tone="warning" />
        <MetricCard label="Down" value={counts.down} hint="Not responding" icon={CircleX} tone="down" />
        <MetricCard label="Active incidents" value={openIncidents?.length ?? 0} hint="Confirmed outages" icon={BellRing} tone={openIncidents?.length ? 'down' : 'up'} />
      </section>

      <section className="panel">
        <header className="panel-header">
          <div>
            <h2>Device status</h2>
            <p>Current state reported by the background monitoring engine.</p>
          </div>
          {devices && <span className="record-count">{devices.length} devices</span>}
        </header>

        {isLoading && !devices ? (
          <StatePanel type="loading" title="Loading devices" message="Reading the latest network state…" />
        ) : error && !devices ? (
          <StatePanel
            type="error"
            title="Device data unavailable"
            message={error}
            action={<button className="button secondary" type="button" onClick={refresh}>Try again</button>}
          />
        ) : devices?.length === 0 ? (
          <StatePanel type="empty" title="No devices yet" message="Add your first device from the Devices page to start monitoring." />
        ) : (
          <>
            {error && <div className="inline-alert" role="alert">{error} Showing the last successful result.</div>}
            <DeviceTable devices={devices ?? []} />
          </>
        )}
      </section>
    </div>
  )
}
