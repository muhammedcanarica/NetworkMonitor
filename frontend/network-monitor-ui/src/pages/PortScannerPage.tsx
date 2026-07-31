import { useEffect, useMemo, useRef, useState } from 'react'
import { Cable, CheckCircle2, LoaderCircle, ShieldCheck, X, XCircle } from 'lucide-react'
import { portScannerApi } from '../api/portScanner'
import { StatePanel } from '../components/ui/StatePanel'
import type { PortScanRequest, PortScanResponse, PortState } from '../types/api'

const COMMON_PORTS = [20, 21, 22, 23, 25, 53, 80, 110, 143, 443, 445, 1433, 3306, 3389, 5432, 5900, 8080]
const MAX_PORTS = 256

type Preset = 'common' | 'custom'
type ResultFilter = 'All' | PortState

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'The TCP port scan could not be completed.'
}

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === 'AbortError'
}

function isValidIpv4Address(value: string) {
  const segments = value.split('.')
  return segments.length === 4 && segments.every((segment) => {
    if (!/^\d{1,3}$/.test(segment)) return false
    const number = Number(segment)
    return number >= 0 && number <= 255
  })
}

function parsePorts(value: string): number[] {
  const values = value.split(',').map((item) => item.trim()).filter(Boolean)
  if (values.length === 0) throw new Error('Enter at least one TCP port.')

  const ports = new Set<number>()
  for (const value of values) {
    const range = /^(\d+)-(\d+)$/.exec(value)
    const singlePort = /^\d+$/.test(value)
    if (!range && !singlePort) throw new Error(`Invalid port value: ${value}.`)

    const start = Number(range?.[1] ?? value)
    const end = Number(range?.[2] ?? value)
    if (!Number.isInteger(start) || !Number.isInteger(end) || start < 1 || end > 65535) {
      throw new Error('TCP ports must be between 1 and 65535.')
    }
    if (end < start) throw new Error(`Invalid port range: ${value}.`)

    for (let port = start; port <= end; port += 1) {
      ports.add(port)
      if (ports.size > MAX_PORTS) {
        throw new Error(`A maximum of ${MAX_PORTS} TCP ports can be scanned at once.`)
      }
    }
  }

  return [...ports].sort((left, right) => left - right)
}

export function PortScannerPage() {
  const [ipAddress, setIpAddress] = useState('')
  const [preset, setPreset] = useState<Preset>('common')
  const [portsInput, setPortsInput] = useState(COMMON_PORTS.join(','))
  const [timeoutMilliseconds, setTimeoutMilliseconds] = useState('1000')
  const [result, setResult] = useState<PortScanResponse | null>(null)
  const [filter, setFilter] = useState<ResultFilter>('All')
  const [isScanning, setIsScanning] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const scanController = useRef<AbortController | null>(null)

  useEffect(() => () => scanController.current?.abort(), [])

  const visibleResults = useMemo(() => result?.results.filter((item) =>
    filter === 'All' || item.state === filter) ?? [], [filter, result])

  const selectPreset = (nextPreset: Preset) => {
    setPreset(nextPreset)
    if (nextPreset === 'common') setPortsInput(COMMON_PORTS.join(','))
  }

  const createRequest = (): PortScanRequest | null => {
    const target = ipAddress.trim()
    const timeout = Number(timeoutMilliseconds)

    if (!isValidIpv4Address(target)) {
      setError('Enter a valid target IPv4 address.')
      return null
    }
    if (!Number.isInteger(timeout) || timeout < 100 || timeout > 10000) {
      setError('Timeout must be between 100 and 10000 milliseconds.')
      return null
    }

    try {
      return { ipAddress: target, ports: parsePorts(portsInput), timeoutMilliseconds: timeout }
    } catch (portError) {
      setError(getErrorMessage(portError))
      return null
    }
  }

  const scanPorts = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setNotice(null)
    const request = createRequest()
    if (!request) return

    const controller = new AbortController()
    scanController.current = controller
    setIsScanning(true)
    setResult(null)
    setFilter('All')

    try {
      setResult(await portScannerApi.scan(request, controller.signal))
    } catch (scanError) {
      if (isAbortError(scanError)) setNotice('TCP port scan cancelled.')
      else setError(getErrorMessage(scanError))
    } finally {
      if (scanController.current === controller) scanController.current = null
      setIsScanning(false)
    }
  }

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">TCP service discovery</span>
          <h1>Port Scanner</h1>
          <p>Check selected TCP ports with bounded connection attempts. UDP and raw packet scanning are not used.</p>
        </div>
      </header>

      {notice && <div className="success-alert" role="status">{notice}</div>}

      <section className="panel port-scan-control-panel">
        <header className="panel-header">
          <div>
            <h2>Scan selected TCP ports</h2>
            <p>Provide a focused port list. Up to {MAX_PORTS} ports are scanned per request.</p>
          </div>
          <Cable size={22} aria-hidden="true" />
        </header>

        <form className="port-scan-form" onSubmit={scanPorts}>
          <label>
            Target IPv4 address
            <input value={ipAddress} onChange={(event) => setIpAddress(event.target.value)} placeholder="192.168.1.10" disabled={isScanning} spellCheck="false" autoComplete="off" />
          </label>
          <label>
            Port preset
            <select value={preset} onChange={(event) => selectPreset(event.target.value as Preset)} disabled={isScanning}>
              <option value="common">Common ports ({COMMON_PORTS.length})</option>
              <option value="custom">Custom port list</option>
            </select>
          </label>
          <label>
            Timeout (ms)
            <input type="number" min={100} max={10000} step={100} value={timeoutMilliseconds} onChange={(event) => setTimeoutMilliseconds(event.target.value)} disabled={isScanning} />
          </label>
          <label className="port-list-field">
            Custom TCP ports
            <input value={portsInput} onChange={(event) => { setPreset('custom'); setPortsInput(event.target.value) }} placeholder="22,80,443,8000-8010" disabled={isScanning} spellCheck="false" autoComplete="off" aria-describedby="port-list-help" />
            <small id="port-list-help">Use commas and ranges, for example: 22,80,443,8000-8010.</small>
          </label>
          <div className="port-scan-submit">
            <button className="button primary" type="submit" disabled={isScanning}>
              {isScanning ? <LoaderCircle className="spin" size={16} /> : <Cable size={16} />}
              {isScanning ? 'Scanning…' : 'Scan ports'}
            </button>
          </div>
        </form>

        <div className="port-scan-security-note">
          <ShieldCheck size={17} aria-hidden="true" />
          <span><strong>Scope:</strong> This tool makes standard TCP connection attempts only. Scan only hosts and networks you are authorized to assess.</span>
        </div>
      </section>

      {error && <section className="panel"><StatePanel type="error" title="TCP port scan could not be completed" message={error} /></section>}

      {isScanning && (
        <section className="panel">
          <StatePanel type="loading" title="Scanning TCP ports" message="Running bounded TCP connection attempts against the selected ports…" action={<button className="button secondary" type="button" onClick={() => scanController.current?.abort()}><X size={15} /> Cancel</button>} />
        </section>
      )}

      {result && (
        <>
          <section className="scanner-summary port-scan-summary" aria-label="Port scan summary">
            <div><span>Scanned</span><strong>{result.scannedPorts}</strong><small>TCP ports</small></div>
            <div><span>Open</span><strong>{result.openPorts}</strong><small>accepted connections</small></div>
            <div><span>Duration</span><strong>{result.durationMs} ms</strong><small>{result.ipAddress}</small></div>
          </section>

          <section className="panel">
            <header className="panel-header">
              <div><h2>Port results</h2><p>Closed means the connection was refused or did not complete within the selected timeout.</p></div>
              <span className="record-count">{visibleResults.length} results</span>
            </header>
            <nav className="port-result-filters" aria-label="Port scan result filters">
              {(['All', 'Open', 'Closed'] as ResultFilter[]).map((value) => (
                <button className={filter === value ? 'active' : ''} type="button" onClick={() => setFilter(value)} key={value}>{value}</button>
              ))}
            </nav>
            <div className="table-scroll">
              <table className="data-table port-scan-table">
                <thead><tr><th>Port</th><th>State</th><th>Service</th><th>Latency</th></tr></thead>
                <tbody>
                  {visibleResults.map((item) => (
                    <tr key={item.port}>
                      <td className="mono">{item.port}</td>
                      <td><span className={`port-state ${item.state.toLowerCase()}`}>{item.state === 'Open' ? <CheckCircle2 size={14} /> : <XCircle size={14} />}{item.state}</span></td>
                      <td>{item.serviceName || 'Unknown'}</td>
                      <td className="mono">{item.latencyMs === null ? '—' : `${item.latencyMs} ms`}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}

      {!result && !isScanning && !error && <section className="panel"><StatePanel type="empty" title="No scan results" message="Choose a target and port list, then run a focused TCP scan." /></section>}
    </div>
  )
}
