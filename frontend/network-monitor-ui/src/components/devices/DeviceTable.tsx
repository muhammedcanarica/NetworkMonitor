import { ExternalLink, Pencil, Power, Trash2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { Device } from '../../types/api'
import { formatLatency, formatRelativeTime } from '../../utils/format'
import { StatusBadge } from '../ui/StatusBadge'

interface DeviceTableProps {
  devices: Device[]
  onEdit?: (device: Device) => void
  onDelete?: (device: Device) => void
  onToggleMonitoring?: (device: Device) => void
  busyDeviceId?: number | null
}

export function DeviceTable({
  devices,
  onEdit,
  onDelete,
  onToggleMonitoring,
  busyDeviceId,
}: DeviceTableProps) {
  const hasActions = Boolean(onEdit || onDelete || onToggleMonitoring)

  return (
    <div className="table-scroll">
      <table className="data-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>IP address</th>
            <th>Status</th>
            <th>Latency</th>
            <th>Last checked</th>
            <th>Last seen</th>
            <th>Monitoring</th>
            <th className="align-right">{hasActions ? 'Actions' : 'Details'}</th>
          </tr>
        </thead>
        <tbody>
          {devices.map((device) => {
            const isBusy = busyDeviceId === device.id

            return (
              <tr key={device.id}>
                <td>
                  <Link className="device-name" to={`/devices/${device.id}`}>
                    {device.name}
                  </Link>
                  {device.description && <small>{device.description}</small>}
                </td>
                <td className="mono">{device.ipAddress}</td>
                <td><StatusBadge status={device.status} /></td>
                <td className="mono">{formatLatency(device.lastLatencyMs)}</td>
                <td title={device.lastCheckedAt ?? undefined}>
                  {formatRelativeTime(device.lastCheckedAt)}
                </td>
                <td title={device.lastSeenAt ?? undefined}>
                  {formatRelativeTime(device.lastSeenAt)}
                </td>
                <td>
                  <span className={`monitor-state ${device.isMonitoringEnabled ? 'enabled' : ''}`}>
                    {device.isMonitoringEnabled ? 'Enabled' : 'Paused'}
                  </span>
                </td>
                <td>
                  <div className="table-actions">
                    {onToggleMonitoring && (
                      <button
                        className="icon-button"
                        type="button"
                        title={device.isMonitoringEnabled ? 'Pause monitoring' : 'Enable monitoring'}
                        aria-label={device.isMonitoringEnabled ? `Pause ${device.name}` : `Monitor ${device.name}`}
                        disabled={isBusy}
                        onClick={() => onToggleMonitoring(device)}
                      >
                        <Power size={16} />
                      </button>
                    )}
                    {onEdit && (
                      <button
                        className="icon-button"
                        type="button"
                        title="Edit device"
                        aria-label={`Edit ${device.name}`}
                        disabled={isBusy}
                        onClick={() => onEdit(device)}
                      >
                        <Pencil size={16} />
                      </button>
                    )}
                    {onDelete && (
                      <button
                        className="icon-button danger"
                        type="button"
                        title="Delete device"
                        aria-label={`Delete ${device.name}`}
                        disabled={isBusy}
                        onClick={() => onDelete(device)}
                      >
                        <Trash2 size={16} />
                      </button>
                    )}
                    <Link className="icon-button" to={`/devices/${device.id}`} aria-label={`View ${device.name}`}>
                      <ExternalLink size={16} />
                    </Link>
                  </div>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
