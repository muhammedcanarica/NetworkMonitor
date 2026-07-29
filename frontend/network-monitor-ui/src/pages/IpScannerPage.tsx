import { useEffect, useRef, useState } from 'react'
import { Check, LoaderCircle, Plus, Radar, ShieldCheck, X } from 'lucide-react'
import { Link } from 'react-router-dom'
import { devicesApi } from '../api/devices'
import { ipScannerApi } from '../api/ipScanner'
import { AddScannedDeviceModal } from '../components/ip-scanner/AddScannedDeviceModal'
import { StatePanel } from '../components/ui/StatePanel'
import type { IpScanHost, IpScanResponse } from '../types/api'

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'İşlem tamamlanamadı.'
}

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === 'AbortError'
}

export function IpScannerPage() {
  const [cidr, setCidr] = useState('127.0.0.0/30')
  const [scanResult, setScanResult] = useState<IpScanResponse | null>(null)
  const [scanError, setScanError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [isScanning, setIsScanning] = useState(false)
  const [selectedHost, setSelectedHost] = useState<IpScanHost | null>(null)
  const [isAdding, setIsAdding] = useState(false)
  const [addError, setAddError] = useState<string | null>(null)
  const scanController = useRef<AbortController | null>(null)

  useEffect(() => () => scanController.current?.abort(), [])

  const handleScan = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const normalizedCidr = cidr.trim()
    if (!normalizedCidr) {
      setScanError('Enter an IPv4 CIDR range to scan.')
      return
    }

    const controller = new AbortController()
    scanController.current = controller
    setIsScanning(true)
    setScanError(null)
    setNotice(null)
    setScanResult(null)

    try {
      setScanResult(await ipScannerApi.scan(normalizedCidr, controller.signal))
    } catch (error) {
      if (isAbortError(error)) {
        setNotice('Scan cancelled.')
      } else {
        setScanError(getErrorMessage(error))
      }
    } finally {
      if (scanController.current === controller) scanController.current = null
      setIsScanning(false)
    }
  }

  const cancelScan = () => scanController.current?.abort()

  const openAddModal = (host: IpScanHost) => {
    setSelectedHost(host)
    setAddError(null)
  }

  const addToMonitoring = async (name: string) => {
    if (!selectedHost) return

    setIsAdding(true)
    setAddError(null)
    try {
      const device = await devicesApi.create({
        name,
        ipAddress: selectedHost.ipAddress,
        description: null,
      })
      setScanResult((current) => current ? {
        ...current,
        results: current.results.map((host) => host.ipAddress === device.ipAddress
          ? { ...host, isAlreadyMonitored: true, deviceId: device.id }
          : host),
      } : current)
      setNotice(`${device.name} added to monitoring.`)
      setSelectedHost(null)
    } catch (error) {
      setAddError(getErrorMessage(error))
    } finally {
      setIsAdding(false)
    }
  }

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Network discovery</span>
          <h1>IP Scanner</h1>
          <p>Discover reachable IPv4 hosts with bounded ICMP checks, then add selected devices to monitoring.</p>
        </div>
      </header>

      {notice && <div className="success-alert" role="status">{notice}</div>}

      <section className="panel scanner-control-panel">
        <header className="panel-header">
          <div>
            <h2>Scan a CIDR range</h2>
            <p>Network and broadcast addresses are excluded where applicable. Up to 1,024 hosts per scan.</p>
          </div>
          <Radar size={22} aria-hidden="true" />
        </header>
        <form className="scanner-form" onSubmit={handleScan}>
          <label htmlFor="scanner-cidr">IPv4 CIDR</label>
          <div className="scanner-input-row">
            <input
              id="scanner-cidr"
              value={cidr}
              onChange={(event) => setCidr(event.target.value)}
              placeholder="192.168.1.0/24"
              spellCheck="false"
              autoComplete="off"
              disabled={isScanning}
              aria-describedby="scanner-security-note"
            />
            <button className="button primary" type="submit" disabled={isScanning || !cidr.trim()}>
              {isScanning ? <LoaderCircle className="spin" size={16} /> : <Radar size={16} />}
              {isScanning ? 'Scanning…' : 'Scan'}
            </button>
            {isScanning && (
              <button className="button secondary" type="button" onClick={cancelScan}>
                <X size={16} /> Cancel
              </button>
            )}
          </div>
        </form>
        <div className="scanner-security-note" id="scanner-security-note">
          <ShieldCheck size={17} aria-hidden="true" />
          <span><strong>Security note:</strong> This tool sends ICMP echo requests only to the CIDR range you provide. It does not scan ports or use stealth discovery.</span>
        </div>
      </section>

      {scanError && (
        <section className="panel">
          <StatePanel type="error" title="Scan could not be completed" message={scanError} />
        </section>
      )}

      {isScanning && (
        <section className="panel">
          <StatePanel type="loading" title="Scanning addresses" message="Checking the requested hosts with controlled parallel ICMP requests…" />
        </section>
      )}

      {scanResult && (
        <>
          <section className="scanner-summary" aria-label="Scan summary">
            <div><span>Scanned</span><strong>{scanResult.scannedAddresses}</strong><small>host addresses</small></div>
            <div><span>Reachable</span><strong>{scanResult.reachableHosts}</strong><small>ICMP responses</small></div>
            <div><span>Duration</span><strong>{scanResult.durationMs} ms</strong><small>{scanResult.cidr}</small></div>
          </section>

          <section className="panel">
            <header className="panel-header">
              <div>
                <h2>Reachable hosts</h2>
                <p>Only hosts that responded to ICMP are listed.</p>
              </div>
              <span className="record-count">{scanResult.results.length} results</span>
            </header>

            {scanResult.results.length === 0 ? (
              <StatePanel type="empty" title="No reachable hosts" message="The scan completed, but no host returned an ICMP response." />
            ) : (
              <div className="table-scroll">
                <table className="data-table scanner-table">
                  <thead>
                    <tr>
                      <th>IP address</th>
                      <th>Host name</th>
                      <th>Latency</th>
                      <th>Monitoring</th>
                      <th className="align-right">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {scanResult.results.map((host) => (
                      <tr key={host.ipAddress}>
                        <td className="mono">{host.ipAddress}</td>
                        <td>{host.hostName || 'Not resolved'}</td>
                        <td className="mono">{host.latencyMs === null ? '—' : `${host.latencyMs} ms`}</td>
                        <td>
                          <span className={`scanner-monitor-state ${host.isAlreadyMonitored ? 'monitored' : ''}`}>
                            {host.isAlreadyMonitored && <Check size={13} />}
                            {host.isAlreadyMonitored ? 'Monitored' : 'Not monitored'}
                          </span>
                        </td>
                        <td className="align-right">
                          {host.isAlreadyMonitored && host.deviceId ? (
                            <Link className="button secondary compact-button" to={`/devices/${host.deviceId}`}>View device</Link>
                          ) : (
                            <button className="button secondary compact-button" type="button" onClick={() => openAddModal(host)}>
                              <Plus size={14} /> Add to monitoring
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}

      {selectedHost && (
        <AddScannedDeviceModal
          host={selectedHost}
          isSaving={isAdding}
          error={addError}
          onClose={() => setSelectedHost(null)}
          onSubmit={addToMonitoring}
        />
      )}
    </div>
  )
}
