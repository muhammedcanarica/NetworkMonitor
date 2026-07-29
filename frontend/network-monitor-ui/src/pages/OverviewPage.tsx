import { useCallback, useMemo } from 'react'
import { Activity, AlertTriangle, CircleX, Server } from 'lucide-react'
import { devicesApi } from '../api/devices'
import { DeviceTable } from '../components/devices/DeviceTable'
import { MetricCard } from '../components/ui/MetricCard'
import { StatePanel } from '../components/ui/StatePanel'
import { usePolling } from '../hooks/usePolling'

export function OverviewPage() {
  const loadDevices = useCallback(
    (signal: AbortSignal) => devicesApi.list(signal),
    [],
  )
  const { data: devices, error, isLoading, isRefreshing, refresh } = usePolling(loadDevices)

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
        <div className={`refresh-indicator ${isRefreshing ? 'is-refreshing' : ''}`}>
          <span aria-hidden="true" />
          {isRefreshing ? 'Refreshing' : 'Live'}
        </div>
      </header>

      <section className="metrics-grid" aria-label="Device summary">
        <MetricCard label="Total devices" value={counts.total} hint="Registered inventory" icon={Server} />
        <MetricCard label="Up" value={counts.up} hint="Responding normally" icon={Activity} tone="up" />
        <MetricCard label="Warning" value={counts.warning} hint="Needs attention" icon={AlertTriangle} tone="warning" />
        <MetricCard label="Down" value={counts.down} hint="Not responding" icon={CircleX} tone="down" />
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
