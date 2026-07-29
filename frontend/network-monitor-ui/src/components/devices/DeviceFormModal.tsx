import { useEffect, useState } from 'react'
import { LoaderCircle, X } from 'lucide-react'
import type {
  CreateDeviceRequest,
  Device,
  UpdateDeviceRequest,
} from '../../types/api'

interface DeviceFormModalProps {
  device: Device | null
  isSaving: boolean
  error: string | null
  onClose: () => void
  onSubmit: (request: CreateDeviceRequest | UpdateDeviceRequest) => Promise<void>
}

interface FormState {
  name: string
  ipAddress: string
  description: string
  isMonitoringEnabled: boolean
}

export function DeviceFormModal({
  device,
  isSaving,
  error,
  onClose,
  onSubmit,
}: DeviceFormModalProps) {
  const [form, setForm] = useState<FormState>({
    name: '',
    ipAddress: '',
    description: '',
    isMonitoringEnabled: true,
  })

  useEffect(() => {
    setForm({
      name: device?.name ?? '',
      ipAddress: device?.ipAddress ?? '',
      description: device?.description ?? '',
      isMonitoringEnabled: device?.isMonitoringEnabled ?? true,
    })
  }, [device])

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const baseRequest = {
      name: form.name.trim(),
      ipAddress: form.ipAddress.trim(),
      description: form.description.trim() || null,
    }

    await onSubmit(
      device
        ? { ...baseRequest, isMonitoringEnabled: form.isMonitoringEnabled }
        : baseRequest,
    )
  }

  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={(event) => {
      if (event.currentTarget === event.target && !isSaving) onClose()
    }}>
      <section className="modal" role="dialog" aria-modal="true" aria-labelledby="device-form-title">
        <header>
          <div>
            <span className="eyebrow">Device configuration</span>
            <h2 id="device-form-title">{device ? 'Edit device' : 'Add device'}</h2>
          </div>
          <button className="icon-button" type="button" onClick={onClose} disabled={isSaving} aria-label="Close">
            <X size={18} />
          </button>
        </header>

        <form onSubmit={handleSubmit}>
          <label>
            Device name
            <input
              autoFocus
              required
              maxLength={100}
              value={form.name}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
              placeholder="Core Router"
            />
          </label>
          <label>
            IP address
            <input
              required
              value={form.ipAddress}
              onChange={(event) => setForm({ ...form, ipAddress: event.target.value })}
              placeholder="192.168.1.1"
              inputMode="decimal"
            />
          </label>
          <label>
            Description <span className="optional">Optional</span>
            <textarea
              maxLength={500}
              value={form.description}
              onChange={(event) => setForm({ ...form, description: event.target.value })}
              placeholder="Location or device role"
              rows={3}
            />
          </label>
          {device && (
            <label className="toggle-row">
              <input
                type="checkbox"
                checked={form.isMonitoringEnabled}
                onChange={(event) => setForm({ ...form, isMonitoringEnabled: event.target.checked })}
              />
              <span>
                <strong>Background monitoring</strong>
                <small>Allow periodic ICMP checks for this device.</small>
              </span>
            </label>
          )}

          {error && <div className="form-error" role="alert">{error}</div>}

          <footer>
            <button className="button secondary" type="button" onClick={onClose} disabled={isSaving}>Cancel</button>
            <button className="button primary" type="submit" disabled={isSaving}>
              {isSaving && <LoaderCircle className="spin" size={16} />}
              {device ? 'Save changes' : 'Add device'}
            </button>
          </footer>
        </form>
      </section>
    </div>
  )
}
