import { useEffect, useRef, useState } from 'react'
import { LoaderCircle, Network, ShieldCheck, X } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { devicesApi } from '../api/devices'
import { topologyApi } from '../api/topology'
import { TopologyGraph } from '../components/topology/TopologyGraph'
import { StatePanel } from '../components/ui/StatePanel'
import type { Device, TopologyDiscoveryResponse } from '../types/api'

const MAX_SELECTED_DEVICES = 32

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Topology discovery could not be completed.'
}

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === 'AbortError'
}

export function TopologyPage() {
  const navigate = useNavigate()
  const [devices, setDevices] = useState<Device[]>([])
  const [selectedIds, setSelectedIds] = useState<number[]>([])
  const [community, setCommunity] = useState('')
  const [timeoutMilliseconds, setTimeoutMilliseconds] = useState('2000')
  const [topology, setTopology] = useState<TopologyDiscoveryResponse | null>(null)
  const [isLoadingDevices, setIsLoadingDevices] = useState(true)
  const [isDiscovering, setIsDiscovering] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const controller = useRef<AbortController | null>(null)

  useEffect(() => {
    const loadController = new AbortController()
    devicesApi.list(loadController.signal)
      .then(setDevices)
      .catch((loadError) => { if (!isAbortError(loadError)) setError(getErrorMessage(loadError)) })
      .finally(() => setIsLoadingDevices(false))
    return () => loadController.abort()
  }, [])

  useEffect(() => () => controller.current?.abort(), [])

  const toggleDevice = (deviceId: number) => {
    setError(null)
    setSelectedIds((current) => {
      if (current.includes(deviceId)) return current.filter((id) => id !== deviceId)
      if (current.length >= MAX_SELECTED_DEVICES) {
        setError(`Select up to ${MAX_SELECTED_DEVICES} devices for one discovery.`)
        return current
      }
      return [...current, deviceId]
    })
  }

  const discover = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setNotice(null)
    const timeout = Number(timeoutMilliseconds)
    if (selectedIds.length === 0) return setError('Select at least one monitored device.')
    if (!community.trim()) return setError('Enter the SNMP community for this one-time discovery.')
    if (!Number.isInteger(timeout) || timeout < 500 || timeout > 10000) return setError('Timeout must be between 500 and 10000 milliseconds.')

    const nextController = new AbortController()
    controller.current = nextController
    setIsDiscovering(true)
    setTopology(null)
    try {
      setTopology(await topologyApi.discover({ deviceIds: selectedIds, community: community.trim(), timeoutMilliseconds: timeout }, nextController.signal))
    } catch (discoveryError) {
      if (isAbortError(discoveryError)) setNotice('Topology discovery cancelled.')
      else setError(getErrorMessage(discoveryError))
    } finally {
      if (controller.current === nextController) controller.current = null
      setIsDiscovering(false)
    }
  }

  return (
    <div className="page topology-page">
      <header className="page-header">
        <div>
          <span className="eyebrow">LLDP network discovery</span>
          <h1>Network Topology</h1>
          <p>Build a read-only topology from LLDP data reported by the monitored devices you select.</p>
        </div>
      </header>

      {notice && <div className="success-alert" role="status">{notice}</div>}
      {error && <div className="form-error" role="alert">{error}</div>}

      <section className="panel topology-control-panel">
        <header className="panel-header">
          <div><h2>Discover selected devices</h2><p>No recursive scanning: only selected monitored devices receive SNMP requests.</p></div>
          <Network size={22} aria-hidden="true" />
        </header>
        {isLoadingDevices ? <StatePanel type="loading" title="Loading monitored devices" message="Preparing the discovery selection…" /> : (
          <form onSubmit={discover}>
            <div className="topology-controls">
              <label>
                SNMP community
                <input type="password" value={community} onChange={(event) => setCommunity(event.target.value)} autoComplete="off" placeholder="Entered only for this discovery" disabled={isDiscovering} />
              </label>
              <label>
                Timeout (ms)
                <input type="number" min={500} max={10000} step={100} value={timeoutMilliseconds} onChange={(event) => setTimeoutMilliseconds(event.target.value)} disabled={isDiscovering} />
              </label>
              <button className="button primary" type="submit" disabled={isDiscovering || devices.length === 0}>
                {isDiscovering ? <LoaderCircle className="spin" size={16} /> : <Network size={16} />}
                {isDiscovering ? 'Discovering…' : 'Discover topology'}
              </button>
            </div>
            <fieldset className="topology-device-picker" disabled={isDiscovering}>
              <legend>Monitored devices <span>{selectedIds.length}/{MAX_SELECTED_DEVICES} selected</span></legend>
              {devices.length === 0 ? <p>No monitored devices are available. Add a device before starting discovery.</p> : devices.map((device) => (
                <label key={device.id} className="topology-device-option">
                  <input type="checkbox" checked={selectedIds.includes(device.id)} onChange={() => toggleDevice(device.id)} />
                  <span><strong>{device.name}</strong><small>{device.ipAddress} · {device.status}</small></span>
                </label>
              ))}
            </fieldset>
          </form>
        )}
        <div className="topology-security-note"><ShieldCheck size={17} aria-hidden="true" /><span><strong>Security:</strong> SNMP community is used only for this request. It is not stored, included in the result, or placed in the URL.</span></div>
      </section>

      {isDiscovering && <section className="panel"><StatePanel type="loading" title="Reading LLDP neighbors" message="Selected devices are queried with bounded concurrency; unavailable devices may return as warnings." action={<button className="button secondary" type="button" onClick={() => controller.current?.abort()}><X size={15} /> Cancel</button>} /></section>}

      {topology && (
        <>
          <section className="scanner-summary topology-summary" aria-label="Topology discovery summary">
            <div><span>Scanned</span><strong>{topology.scannedDevices}</strong><small>selected devices</small></div>
            <div><span>Successful</span><strong>{topology.successfulDevices}</strong><small>{topology.failedDevices} failed</small></div>
            <div><span>Links</span><strong>{topology.edges.length}</strong><small>{topology.durationMs} ms</small></div>
          </section>
          {topology.warnings.length > 0 && <div className="inline-alert">{topology.warnings.join(' ')}</div>}
          {topology.nodes.length === 0 ? <section className="panel"><StatePanel type="empty" title="No topology nodes discovered" message="No selected devices were available to add to the topology." /></section> : (
            <section className="panel topology-graph-panel">
              <header className="panel-header"><div><h2>LLDP topology</h2><p>{topology.edges.length === 0 ? 'No links discovered. Selected devices may not expose LLDP neighbors.' : 'Select a managed node to open its device detail page.'}</p></div><span className="protocol-badge">LLDP</span></header>
              <TopologyGraph nodes={topology.nodes} edges={topology.edges} onManagedNodeClick={(deviceId) => navigate(`/devices/${deviceId}`)} />
            </section>
          )}
        </>
      )}
    </div>
  )
}
