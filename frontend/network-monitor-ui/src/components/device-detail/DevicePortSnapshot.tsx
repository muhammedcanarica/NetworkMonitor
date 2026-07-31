import { useEffect, useRef, useState } from 'react'
import { Cable, LoaderCircle, X } from 'lucide-react'
import { portScannerApi } from '../../api/portScanner'
import type { PortScanResponse } from '../../types/api'
import { COMMON_TCP_PORTS } from '../../utils/commonPorts'

export function DevicePortSnapshot({ ipAddress }: { ipAddress: string }) {
  const [result, setResult] = useState<PortScanResponse | null>(null); const [loading, setLoading] = useState(false); const [error, setError] = useState<string | null>(null); const controller = useRef<AbortController | null>(null)
  useEffect(() => () => controller.current?.abort(), [])
  const scan = async () => { const next = new AbortController(); controller.current = next; setLoading(true); setError(null); try { setResult(await portScannerApi.scan({ ipAddress, ports: COMMON_TCP_PORTS, timeoutMilliseconds: 1000 }, next.signal)) } catch (scanError) { if (!(scanError instanceof DOMException && scanError.name === 'AbortError')) setError(scanError instanceof Error ? scanError.message : 'Common TCP service scan failed.') } finally { if (controller.current === next) controller.current = null; setLoading(false) } }
  const rows = result?.results.slice().sort((a, b) => (a.state === b.state ? a.port - b.port : a.state === 'Open' ? -1 : 1)) ?? []
  return <section className="panel device-intelligence-card"><header className="panel-header"><div><h2>Common TCP Services</h2><p>On-demand snapshot of {COMMON_TCP_PORTS.length} common TCP ports.</p></div><Cable size={19} aria-hidden="true" /></header><div className="device-intelligence-actions"><button className="button primary compact-button" type="button" onClick={() => void scan()} disabled={loading}>{loading ? <LoaderCircle className="spin" size={15} /> : <Cable size={15} />}{loading ? 'Scanning…' : 'Scan Common Ports'}</button>{loading && <button className="button secondary compact-button" type="button" onClick={() => controller.current?.abort()}><X size={15} /> Cancel</button>}</div>{error && <div className="device-intelligence-error">{error}</div>}{result && <div className="table-scroll"><table className="data-table device-port-table"><thead><tr><th>Port</th><th>Service</th><th>State</th><th>Latency</th></tr></thead><tbody>{rows.map((item) => <tr key={item.port}><td>{item.port}</td><td>{item.serviceName || '—'}</td><td><span className={`port-state ${item.state.toLowerCase()}`}>{item.state}</span></td><td>{item.latencyMs === null ? '—' : `${item.latencyMs} ms`}</td></tr>)}</tbody></table></div>}</section>
}
