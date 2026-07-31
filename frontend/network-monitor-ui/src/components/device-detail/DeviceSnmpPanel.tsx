import { useEffect, useRef, useState } from 'react'
import { LoaderCircle, Network, ShieldCheck, X } from 'lucide-react'
import { snmpApi } from '../../api/snmp'
import { CredentialSourceSelector, type CredentialSource } from '../credentials/CredentialSourceSelector'
import type { SnmpInterface, SnmpSystemInfo } from '../../types/api'

export function DeviceSnmpPanel({ ipAddress }: { ipAddress: string }) {
  const [community, setCommunity] = useState('')
  const [credentialSource, setCredentialSource] = useState<CredentialSource>('manual')
  const [credentialId, setCredentialId] = useState<number | null>(null)
  const [data, setData] = useState<{ system: SnmpSystemInfo; interfaces: SnmpInterface[] } | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const controller = useRef<AbortController | null>(null)

  useEffect(() => () => controller.current?.abort(), [])

  const load = async () => {
    if (credentialSource === 'manual' && !community.trim()) return setError('Enter an SNMP community for this request.')
    if (credentialSource === 'saved' && credentialId === null) return setError('Select a saved SNMP credential.')

    const next = new AbortController()
    controller.current = next
    setError(null)
    setLoading(true)
    try {
      const request = {
        ipAddress,
        community: credentialSource === 'manual' ? community.trim() : null,
        credentialId: credentialSource === 'saved' ? credentialId : null,
        timeoutMilliseconds: 2000,
      }
      const [system, interfaces] = await Promise.all([
        snmpApi.systemInfo(request, next.signal),
        snmpApi.interfaces(request, next.signal),
      ])
      setData({ system, interfaces })
    } catch (requestError) {
      if (!(requestError instanceof DOMException && requestError.name === 'AbortError')) {
        setError(requestError instanceof Error ? requestError.message : 'SNMP data could not be loaded.')
      }
    } finally {
      if (controller.current === next) controller.current = null
      setLoading(false)
    }
  }

  return <section className="panel device-intelligence-card">
    <header className="panel-header"><div><h2>Live SNMP</h2><p>On-demand SNMP v2c system and interface snapshot.</p></div><Network size={19} aria-hidden="true" /></header>
    <div className="device-snmp-form">
      <CredentialSourceSelector type="SnmpV2Community" source={credentialSource} credentialId={credentialId} onSourceChange={setCredentialSource} onCredentialChange={setCredentialId} disabled={loading} />
      {credentialSource === 'manual' && <label>Community<input type="password" value={community} onChange={(event) => setCommunity(event.target.value)} autoComplete="off" placeholder="Used only for this request" disabled={loading} /></label>}
      <button className="button primary compact-button" type="button" onClick={() => void load()} disabled={loading}>{loading ? <LoaderCircle className="spin" size={15} /> : <Network size={15} />}{loading ? 'Loading…' : 'Load SNMP Data'}</button>
      {loading && <button className="button secondary compact-button" type="button" onClick={() => controller.current?.abort()}><X size={15} /> Cancel</button>}
    </div>
    <div className="device-security-copy"><ShieldCheck size={15} /> Manual communities are not stored; saved secrets never leave the server.</div>
    {error && <div className="device-intelligence-error">{error}</div>}
    {data && <><div className="device-snmp-info">{[['Name', data.system.sysName], ['Description', data.system.sysDescription], ['Location', data.system.sysLocation], ['Contact', data.system.sysContact], ['Uptime', data.system.sysUpTimeTicks?.toString()], ['Object ID', data.system.sysObjectId]].map(([label, value]) => <div key={label}><span>{label}</span><strong>{value || '—'}</strong></div>)}</div><div className="table-scroll"><table className="data-table device-interface-table"><thead><tr><th>Interface</th><th>Description</th><th>Admin</th><th>Operational</th><th>Speed</th></tr></thead><tbody>{data.interfaces.map((item) => <tr key={item.index}><td>{item.index}</td><td>{item.description || '—'}</td><td>{item.adminStatus}</td><td><span className={`snmp-status status-${item.operStatus.toLowerCase()}`}>{item.operStatus}</span></td><td>{item.speedBitsPerSecond ? `${Math.round(item.speedBitsPerSecond / 1_000_000)} Mbps` : '—'}</td></tr>)}</tbody></table></div></>}
  </section>
}
