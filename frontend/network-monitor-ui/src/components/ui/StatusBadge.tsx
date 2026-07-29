import type { DeviceStatus } from '../../types/api'

export function StatusBadge({ status }: { status: DeviceStatus }) {
  return (
    <span className={`status-badge status-${status.toLowerCase()}`}>
      <span aria-hidden="true" />
      {status}
    </span>
  )
}
