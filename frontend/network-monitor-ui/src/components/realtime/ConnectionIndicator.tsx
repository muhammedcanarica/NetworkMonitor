import type { MonitoringConnectionStatus } from '../../realtime/monitoringConnection'
import { useRealtimeConnection } from '../../realtime/useRealtime'

interface ConnectionCopy {
  label: string
  detail: string
}

const connectionCopy: Record<MonitoringConnectionStatus, ConnectionCopy> = {
  connected: { label: 'Live', detail: 'SignalR connected' },
  connecting: { label: 'Connecting', detail: 'Opening realtime channel' },
  reconnecting: { label: 'Reconnecting', detail: 'REST sync will follow' },
  disconnected: { label: 'Offline', detail: '30-second REST fallback' },
}

export function ConnectionIndicator({
  compact = false,
  isSyncing = false,
}: {
  compact?: boolean
  isSyncing?: boolean
}) {
  const { status } = useRealtimeConnection()
  const copy = connectionCopy[status]

  return (
    <div
      className={`connection-indicator connection-${status} ${compact ? 'compact' : ''} ${isSyncing ? 'is-syncing' : ''}`}
      role="status"
      aria-label={`Realtime connection: ${copy.label}`}
    >
      <span className="connection-dot" aria-hidden="true" />
      <span>
        <strong>{isSyncing ? 'Syncing' : copy.label}</strong>
        {!compact && <small>{copy.detail}</small>}
      </span>
    </div>
  )
}
