import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Activity, LoaderCircle, Pause, Settings2, X } from 'lucide-react'
import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { credentialsApi } from '../../api/credentials'
import { snmpMonitoringApi } from '../../api/snmpMonitoring'
import { StatePanel } from '../ui/StatePanel'
import type { InterfaceTrafficHistory, InterfaceTrafficSummary, NetworkCredential, SnmpInterface, SnmpMonitoringProfile } from '../../types/api'
import { formatLocalDateTime, formatRelativeTime, formatTime } from '../../utils/format'

const ranges = [1, 6, 24] as const

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Bandwidth monitoring request failed.'
}

function formatRate(value: number | null) {
  if (value === null) return 'Collecting baseline'
  if (value >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(2)} Gbps`
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(2)} Mbps`
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)} Kbps`
  return `${value.toFixed(0)} bps`
}

export function DeviceBandwidthPanel({ deviceId }: { deviceId: number }) {
  const [profile, setProfile] = useState<SnmpMonitoringProfile | null>(null)
  const [summary, setSummary] = useState<InterfaceTrafficSummary[]>([])
  const [credentials, setCredentials] = useState<NetworkCredential[]>([])
  const [credentialId, setCredentialId] = useState<number | null>(null)
  const [interfaces, setInterfaces] = useState<SnmpInterface[]>([])
  const [selectedIndexes, setSelectedIndexes] = useState<number[]>([])
  const [selectedInterface, setSelectedInterface] = useState<number | null>(null)
  const [history, setHistory] = useState<InterfaceTrafficHistory | null>(null)
  const [hours, setHours] = useState<(typeof ranges)[number]>(1)
  const [showSetup, setShowSetup] = useState(false)
  const [busy, setBusy] = useState<'load' | 'interfaces' | 'save' | 'disable' | null>('load')
  const [error, setError] = useState<string | null>(null)
  const request = useRef<AbortController | null>(null)

  const refreshSummary = useCallback(async (signal?: AbortSignal) => {
    const nextSummary = await snmpMonitoringApi.summary(deviceId, signal)
    setSummary(nextSummary)
    setSelectedInterface((current) => current ?? nextSummary[0]?.interfaceIndex ?? null)
  }, [deviceId])

  useEffect(() => {
    const controller = new AbortController()
    request.current = controller
    Promise.all([snmpMonitoringApi.get(deviceId, controller.signal), snmpMonitoringApi.summary(deviceId, controller.signal), credentialsApi.list(controller.signal)])
      .then(([nextProfile, nextSummary, items]) => {
        setProfile(nextProfile)
        setSummary(nextSummary)
        setCredentials(items.filter((item) => item.type === 'SnmpV2Community'))
        setSelectedInterface(nextSummary[0]?.interfaceIndex ?? null)
        if (nextProfile) {
          setCredentialId(nextProfile.credentialId)
          setSelectedIndexes(nextProfile.interfaces.filter((item) => item.isEnabled).map((item) => item.interfaceIndex))
        }
      })
      .catch((loadError) => { if (!(loadError instanceof DOMException && loadError.name === 'AbortError')) setError(errorMessage(loadError)) })
      .finally(() => setBusy(null))
    const timer = window.setInterval(() => void refreshSummary().catch(() => undefined), 30_000)
    return () => { controller.abort(); request.current?.abort(); window.clearInterval(timer) }
  }, [deviceId, refreshSummary])

  useEffect(() => {
    if (selectedInterface === null) { setHistory(null); return }
    const controller = new AbortController()
    snmpMonitoringApi.history(deviceId, selectedInterface, hours, controller.signal)
      .then(setHistory)
      .catch((historyError) => { if (!(historyError instanceof DOMException && historyError.name === 'AbortError')) setError(errorMessage(historyError)) })
    return () => controller.abort()
  }, [deviceId, selectedInterface, hours, summary])

  const loadInterfaces = async () => {
    if (credentialId === null) return setError('Select a saved SNMP credential.')
    const controller = new AbortController(); request.current = controller; setBusy('interfaces'); setError(null)
    try { setInterfaces(await snmpMonitoringApi.discoverInterfaces(deviceId, credentialId, controller.signal)) }
    catch (loadError) { if (!(loadError instanceof DOMException && loadError.name === 'AbortError')) setError(errorMessage(loadError)) }
    finally { if (request.current === controller) request.current = null; setBusy(null) }
  }

  const save = async () => {
    if (credentialId === null) return setError('Select a saved SNMP credential.')
    if (selectedIndexes.length === 0) return setError('Select at least one interface.')
    setBusy('save'); setError(null)
    try { const next = await snmpMonitoringApi.update(deviceId, { credentialId, isEnabled: true, interfaceIndexes: selectedIndexes }); setProfile(next); setShowSetup(false); await refreshSummary() }
    catch (saveError) { setError(errorMessage(saveError)) }
    finally { setBusy(null) }
  }

  const disable = async () => {
    setBusy('disable'); setError(null)
    try { await snmpMonitoringApi.disable(deviceId); setProfile((current) => current ? { ...current, isEnabled: false } : current) }
    catch (disableError) { setError(errorMessage(disableError)) }
    finally { setBusy(null) }
  }

  const chartData = useMemo(() => (history?.samples ?? []).map((sample) => ({
    timestamp: sample.timestamp,
    time: formatTime(sample.timestamp),
    inboundMbps: sample.inboundBitsPerSecond === null ? null : sample.inboundBitsPerSecond / 1_000_000,
    outboundMbps: sample.outboundBitsPerSecond === null ? null : sample.outboundBitsPerSecond / 1_000_000,
  })), [history])

  const setupVisible = showSetup || profile === null
  return <section className="panel bandwidth-panel">
    <header className="panel-header"><div><h2>Interface Traffic</h2><p>Background SNMP v2c traffic samples from selected 64-bit interface counters.</p></div><Activity size={19} /></header>
    {error && <div className="device-intelligence-error">{error}</div>}
    {busy === 'load' ? <StatePanel type="loading" title="Loading bandwidth monitoring" message="Reading the current configuration and latest samples…" /> : setupVisible ? <div className="bandwidth-setup">
      <label>Saved SNMP credential<select value={credentialId ?? ''} onChange={(event) => { setCredentialId(event.target.value ? Number(event.target.value) : null); setInterfaces([]) }} disabled={busy !== null}><option value="">Select a credential…</option>{credentials.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
      <div className="bandwidth-setup-actions"><button className="button secondary" type="button" disabled={busy !== null || credentialId === null} onClick={() => void loadInterfaces()}>{busy === 'interfaces' ? <LoaderCircle className="spin" size={15} /> : <Settings2 size={15} />}Load Interfaces</button>{profile && <button className="button secondary" type="button" onClick={() => setShowSetup(false)}><X size={15} /> Cancel</button>}</div>
      {interfaces.length > 0 && <div className="bandwidth-interface-picker">{interfaces.map((item) => <label key={item.index}><input type="checkbox" checked={selectedIndexes.includes(item.index)} onChange={() => setSelectedIndexes((current) => current.includes(item.index) ? current.filter((index) => index !== item.index) : [...current, item.index])} /><span><strong>{item.name || item.description || `Interface ${item.index}`}</strong><small>ifIndex {item.index} · {item.operStatus} · {item.speedBitsPerSecond ? formatRate(item.speedBitsPerSecond) : 'Speed unavailable'}{item.description && item.description !== item.name ? ` · ${item.description}` : ''}</small></span></label>)}</div>}
      {interfaces.length > 0 && <button className="button primary" type="button" disabled={busy !== null || selectedIndexes.length === 0} onClick={() => void save()}>{busy === 'save' ? <LoaderCircle className="spin" size={15} /> : <Activity size={15} />} Enable Monitoring</button>}
      <p className="device-security-copy">Background monitoring accepts saved SNMP credentials only. Community values never enter browser state.</p>
    </div> : <>
      <div className="bandwidth-toolbar"><span className={`protocol-badge ${profile.isEnabled ? '' : 'muted'}`}>{profile.isEnabled ? 'MONITORING ACTIVE' : 'MONITORING PAUSED'}</span><div><button className="button secondary compact-button" type="button" onClick={() => setShowSetup(true)}><Settings2 size={15} /> Configure</button>{profile.isEnabled && <button className="button secondary compact-button" type="button" disabled={busy !== null} onClick={() => void disable()}><Pause size={15} /> Disable</button>}</div></div>
      {summary.length === 0 ? <StatePanel type="empty" title={profile.isEnabled ? 'Collecting first samples' : 'Monitoring is paused'} message={profile.isEnabled ? 'The first poll creates a baseline; rates appear after the next valid sample.' : 'Existing history is preserved. Configure monitoring to resume collection.'} /> : <div className="table-scroll"><table className="data-table"><thead><tr><th>Interface</th><th>Status</th><th>In</th><th>Out</th><th>Last sample</th></tr></thead><tbody>{summary.map((item) => <tr className={selectedInterface === item.interfaceIndex ? 'selected-row' : ''} key={item.interfaceIndex} onClick={() => setSelectedInterface(item.interfaceIndex)}><td><strong>{item.interfaceName}</strong><small>ifIndex {item.interfaceIndex}</small></td><td>{item.operStatus ?? 'Waiting'}</td><td>{formatRate(item.inboundBitsPerSecond)}</td><td>{formatRate(item.outboundBitsPerSecond)}</td><td title={formatLocalDateTime(item.lastSampleAt)}>{item.lastSampleAt ? formatRelativeTime(item.lastSampleAt) : 'Waiting'}</td></tr>)}</tbody></table></div>}
      {selectedInterface !== null && <div className="bandwidth-chart-section"><div className="bandwidth-chart-header"><strong>{summary.find((item) => item.interfaceIndex === selectedInterface)?.interfaceName ?? `Interface ${selectedInterface}`}</strong><div className="query-mode-switch">{ranges.map((range) => <button type="button" className={hours === range ? 'active' : ''} key={range} onClick={() => setHours(range)}>{range}h</button>)}</div></div>{chartData.length === 0 ? <StatePanel type="empty" title="No rate history" message="Valid inbound and outbound rates will appear after two consecutive samples." /> : <div className="chart-container"><ResponsiveContainer width="100%" height="100%"><LineChart data={chartData}><CartesianGrid strokeDasharray="4 4" vertical={false} stroke="rgba(148,163,184,.14)" /><XAxis dataKey="time" tick={{ fill: '#738297', fontSize: 11 }} axisLine={false} tickLine={false} /><YAxis unit=" Mbps" tick={{ fill: '#738297', fontSize: 11 }} axisLine={false} tickLine={false} width={75} /><Tooltip contentStyle={{ background: '#111a27', border: '1px solid #27364b', borderRadius: 10 }} labelFormatter={(_, payload) => payload[0]?.payload?.timestamp ? formatLocalDateTime(payload[0].payload.timestamp) : ''} /><Line type="monotone" dataKey="inboundMbps" name="Inbound Mbps" stroke="#30c7d7" dot={false} connectNulls={false} /><Line type="monotone" dataKey="outboundMbps" name="Outbound Mbps" stroke="#8b5cf6" dot={false} connectNulls={false} /></LineChart></ResponsiveContainer></div>}</div>}
    </>}
  </section>
}
