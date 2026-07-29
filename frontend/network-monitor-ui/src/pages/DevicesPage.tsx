import { useCallback, useEffect, useState } from 'react'
import { Plus, Server } from 'lucide-react'
import { devicesApi } from '../api/devices'
import { DeviceFormModal } from '../components/devices/DeviceFormModal'
import { DeviceTable } from '../components/devices/DeviceTable'
import { StatePanel } from '../components/ui/StatePanel'
import type { CreateDeviceRequest, Device, UpdateDeviceRequest } from '../types/api'

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'İşlem tamamlanamadı.'
}

export function DevicesPage() {
  const [devices, setDevices] = useState<Device[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [isFormOpen, setIsFormOpen] = useState(false)
  const [editingDevice, setEditingDevice] = useState<Device | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [busyDeviceId, setBusyDeviceId] = useState<number | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const loadDevices = useCallback(async () => {
    setLoadError(null)
    try {
      setDevices(await devicesApi.list())
    } catch (error) {
      setLoadError(getErrorMessage(error))
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadDevices()
  }, [loadDevices])

  const openCreateForm = () => {
    setEditingDevice(null)
    setFormError(null)
    setIsFormOpen(true)
  }

  const openEditForm = (device: Device) => {
    setEditingDevice(device)
    setFormError(null)
    setIsFormOpen(true)
  }

  const handleSubmit = async (request: CreateDeviceRequest | UpdateDeviceRequest) => {
    setIsSaving(true)
    setFormError(null)

    try {
      if (editingDevice) {
        await devicesApi.update(editingDevice.id, request as UpdateDeviceRequest)
        setNotice(`${request.name} updated.`)
      } else {
        await devicesApi.create(request)
        setNotice(`${request.name} added.`)
      }
      setIsFormOpen(false)
      await loadDevices()
    } catch (error) {
      setFormError(getErrorMessage(error))
    } finally {
      setIsSaving(false)
    }
  }

  const handleDelete = async (device: Device) => {
    const confirmed = window.confirm(
      `Delete ${device.name}? Its monitoring history will also be removed.`,
    )
    if (!confirmed) return

    setBusyDeviceId(device.id)
    setNotice(null)
    try {
      await devicesApi.remove(device.id)
      setNotice(`${device.name} deleted.`)
      await loadDevices()
    } catch (error) {
      setLoadError(getErrorMessage(error))
    } finally {
      setBusyDeviceId(null)
    }
  }

  const handleToggleMonitoring = async (device: Device) => {
    setBusyDeviceId(device.id)
    setNotice(null)
    try {
      await devicesApi.update(device.id, {
        name: device.name,
        ipAddress: device.ipAddress,
        description: device.description,
        isMonitoringEnabled: !device.isMonitoringEnabled,
      })
      setNotice(`Monitoring ${device.isMonitoringEnabled ? 'paused' : 'enabled'} for ${device.name}.`)
      await loadDevices()
    } catch (error) {
      setLoadError(getErrorMessage(error))
    } finally {
      setBusyDeviceId(null)
    }
  }

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Inventory management</span>
          <h1>Devices</h1>
          <p>Add endpoints, update their configuration, and control background monitoring.</p>
        </div>
        <button className="button primary" type="button" onClick={openCreateForm}>
          <Plus size={17} /> Add device
        </button>
      </header>

      {notice && <div className="success-alert" role="status">{notice}</div>}

      <section className="panel">
        <header className="panel-header">
          <div>
            <h2>Device inventory</h2>
            <p>All configured targets and their latest status.</p>
          </div>
          <span className="record-count">{devices.length} devices</span>
        </header>

        {isLoading ? (
          <StatePanel type="loading" title="Loading inventory" message="Fetching configured devices…" />
        ) : loadError && devices.length === 0 ? (
          <StatePanel
            type="error"
            title="Inventory unavailable"
            message={loadError}
            action={<button className="button secondary" type="button" onClick={() => void loadDevices()}>Try again</button>}
          />
        ) : devices.length === 0 ? (
          <StatePanel
            type="empty"
            title="No devices configured"
            message="Create a device to begin collecting latency and uptime history."
            action={<button className="button primary" type="button" onClick={openCreateForm}><Server size={16} /> Add first device</button>}
          />
        ) : (
          <>
            {loadError && <div className="inline-alert" role="alert">{loadError}</div>}
            <DeviceTable
              devices={devices}
              busyDeviceId={busyDeviceId}
              onEdit={openEditForm}
              onDelete={(device) => void handleDelete(device)}
              onToggleMonitoring={(device) => void handleToggleMonitoring(device)}
            />
          </>
        )}
      </section>

      {isFormOpen && (
        <DeviceFormModal
          device={editingDevice}
          error={formError}
          isSaving={isSaving}
          onClose={() => setIsFormOpen(false)}
          onSubmit={handleSubmit}
        />
      )}
    </div>
  )
}
