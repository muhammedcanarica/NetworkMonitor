import { useEffect, useState } from 'react'
import { LoaderCircle, X } from 'lucide-react'
import type { IpScanHost } from '../../types/api'

interface AddScannedDeviceModalProps {
  host: IpScanHost
  isSaving: boolean
  error: string | null
  onClose: () => void
  onSubmit: (name: string) => Promise<void>
}

export function AddScannedDeviceModal({
  host,
  isSaving,
  error,
  onClose,
  onSubmit,
}: AddScannedDeviceModalProps) {
  const [name, setName] = useState('')

  useEffect(() => {
    setName(host.hostName || `Device ${host.ipAddress}`)
  }, [host])

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    await onSubmit(name.trim())
  }

  return (
    <div
      className="modal-backdrop"
      role="presentation"
      onMouseDown={(event) => {
        if (event.currentTarget === event.target && !isSaving) onClose()
      }}
    >
      <section className="modal" role="dialog" aria-modal="true" aria-labelledby="scanner-device-title">
        <header>
          <div>
            <span className="eyebrow">Discovered host</span>
            <h2 id="scanner-device-title">Add to monitoring</h2>
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
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder={`Device ${host.ipAddress}`}
            />
          </label>
          <label>
            IP address
            <input value={host.ipAddress} readOnly aria-readonly="true" />
          </label>

          {error && <div className="form-error" role="alert">{error}</div>}

          <footer>
            <button className="button secondary" type="button" onClick={onClose} disabled={isSaving}>Cancel</button>
            <button className="button primary" type="submit" disabled={isSaving || !name.trim()}>
              {isSaving && <LoaderCircle className="spin" size={16} />}
              Add device
            </button>
          </footer>
        </form>
      </section>
    </div>
  )
}
