import { useEffect, useRef, useState } from 'react'
import {
  Braces,
  Cable,
  DatabaseZap,
  LoaderCircle,
  Play,
  ShieldCheck,
  X,
} from 'lucide-react'
import { devicesApi } from '../api/devices'
import { useSearchParams } from 'react-router-dom'
import { snmpApi } from '../api/snmp'
import { CredentialSourceSelector, type CredentialSource } from '../components/credentials/CredentialSourceSelector'
import { StatePanel } from '../components/ui/StatePanel'
import type {
  Device,
  SnmpConnectionRequest,
  SnmpInterface,
  SnmpSystemInfo,
  SnmpValue,
  SnmpWalkResponse,
} from '../types/api'

type ExplorerTab = 'overview' | 'interfaces' | 'custom'
type QueryMode = 'get' | 'walk'
type Operation = 'system' | 'interfaces' | 'query'

interface ConnectionForm {
  ipAddress: string
  community: string
  timeoutMilliseconds: string
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'İşlem tamamlanamadı.'
}

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === 'AbortError'
}

function formatUptime(ticks: number | null) {
  if (ticks === null) return 'Unavailable'
  const totalSeconds = Math.floor(ticks / 100)
  const days = Math.floor(totalSeconds / 86400)
  const hours = Math.floor((totalSeconds % 86400) / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  return `${days}d ${hours}h ${minutes}m (${ticks.toLocaleString()} ticks)`
}

function formatSpeed(bitsPerSecond: number | null) {
  if (bitsPerSecond === null) return '—'
  if (bitsPerSecond >= 1_000_000_000) return `${bitsPerSecond / 1_000_000_000} Gbps`
  if (bitsPerSecond >= 1_000_000) return `${bitsPerSecond / 1_000_000} Mbps`
  if (bitsPerSecond >= 1_000) return `${bitsPerSecond / 1_000} Kbps`
  return `${bitsPerSecond} bps`
}

function InfoItem({ label, value }: { label: string; value: string | null }) {
  return (
    <div>
      <span>{label}</span>
      <strong title={value ?? undefined}>{value || 'Unavailable'}</strong>
    </div>
  )
}

export function SnmpExplorerPage() {
  const [searchParams] = useSearchParams()
  const [devices, setDevices] = useState<Device[]>([])
  const [deviceLoadError, setDeviceLoadError] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<ExplorerTab>('overview')
  const [connection, setConnection] = useState<ConnectionForm>({
    ipAddress: searchParams.get('ip')?.trim() ?? '',
    community: '',
    timeoutMilliseconds: '2000',
  })
  const [credentialSource, setCredentialSource] = useState<CredentialSource>('manual')
  const [credentialId, setCredentialId] = useState<number | null>(null)
  const [operation, setOperation] = useState<Operation | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [systemInfo, setSystemInfo] = useState<SnmpSystemInfo | null>(null)
  const [interfaces, setInterfaces] = useState<SnmpInterface[] | null>(null)
  const [queryMode, setQueryMode] = useState<QueryMode>('get')
  const [oid, setOid] = useState('1.3.6.1.2.1.1.5.0')
  const [getResult, setGetResult] = useState<SnmpValue | null>(null)
  const [walkResult, setWalkResult] = useState<SnmpWalkResponse | null>(null)
  const requestController = useRef<AbortController | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    devicesApi.list(controller.signal)
      .then(setDevices)
      .catch((loadError: unknown) => {
        if (!isAbortError(loadError)) setDeviceLoadError(getErrorMessage(loadError))
      })
    return () => {
      controller.abort()
      requestController.current?.abort()
    }
  }, [])

  useEffect(() => {
    const queryIpAddress = searchParams.get('ip')?.trim()
    if (queryIpAddress) {
      setConnection((current) => ({ ...current, ipAddress: queryIpAddress }))
    }
  }, [searchParams])

  const createRequest = (): SnmpConnectionRequest | null => {
    const timeout = Number(connection.timeoutMilliseconds)
    if (!connection.ipAddress.trim()) {
      setError('Enter a target IPv4 or IPv6 address.')
      return null
    }
    if (credentialSource === 'manual' && !connection.community.trim()) {
      setError('Community is required.')
      return null
    }
    if (credentialSource === 'saved' && credentialId === null) {
      setError('Select a saved SNMP credential.')
      return null
    }
    if (!Number.isInteger(timeout) || timeout < 500 || timeout > 10000) {
      setError('Timeout must be between 500 and 10000 milliseconds.')
      return null
    }

    return {
      ipAddress: connection.ipAddress.trim(),
      community: credentialSource === 'manual' ? connection.community : null,
      credentialId: credentialSource === 'saved' ? credentialId : null,
      timeoutMilliseconds: timeout,
    }
  }

  const execute = async <T,>(
    currentOperation: Operation,
    action: (request: SnmpConnectionRequest, signal: AbortSignal) => Promise<T>,
    onSuccess: (result: T) => void,
  ) => {
    const request = createRequest()
    if (!request) return

    const controller = new AbortController()
    requestController.current = controller
    setOperation(currentOperation)
    setError(null)
    setNotice(null)

    try {
      onSuccess(await action(request, controller.signal))
    } catch (queryError) {
      if (isAbortError(queryError)) setNotice('SNMP query cancelled.')
      else setError(getErrorMessage(queryError))
    } finally {
      if (requestController.current === controller) requestController.current = null
      setOperation(null)
    }
  }

  const querySystemInfo = () => void execute(
    'system',
    snmpApi.systemInfo,
    setSystemInfo,
  )

  const queryInterfaces = () => void execute(
    'interfaces',
    snmpApi.interfaces,
    setInterfaces,
  )

  const runCustomQuery = () => {
    const normalizedOid = oid.trim()
    if (!normalizedOid) {
      setError('Enter an OID to query.')
      return
    }

    if (queryMode === 'get') {
      void execute(
        'query',
        (request, signal) => snmpApi.get({ ...request, oid: normalizedOid }, signal),
        (result) => {
          setGetResult(result)
          setWalkResult(null)
        },
      )
    } else {
      void execute(
        'query',
        (request, signal) => snmpApi.walk({ ...request, rootOid: normalizedOid }, signal),
        (result) => {
          setWalkResult(result)
          setGetResult(null)
        },
      )
    }
  }

  const selectDevice = (ipAddress: string) => {
    if (ipAddress) setConnection((current) => ({ ...current, ipAddress }))
  }

  const isBusy = operation !== null

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Read-only device inspection</span>
          <h1>SNMP Explorer</h1>
          <p>Query SNMP v2c system data, inspect interfaces, or run bounded GET and WALK requests.</p>
        </div>
      </header>

      {notice && <div className="success-alert" role="status">{notice}</div>}

      <section className="panel snmp-connection-panel">
        <header className="panel-header">
          <div>
            <h2>SNMP connection</h2>
            <p>Use a one-time manual community or a saved encrypted credential.</p>
          </div>
          <span className="protocol-badge">SNMP v2c · READ ONLY</span>
        </header>

        <div className="snmp-connection-grid">
          <CredentialSourceSelector
            type="SnmpV2Community"
            source={credentialSource}
            credentialId={credentialId}
            onSourceChange={setCredentialSource}
            onCredentialChange={setCredentialId}
            disabled={isBusy}
          />
          <label>
            Inventory device <span className="optional">Optional</span>
            <select value="" onChange={(event) => selectDevice(event.target.value)} disabled={isBusy}>
              <option value="">Select a configured device…</option>
              {devices.map((device) => (
                <option value={device.ipAddress} key={device.id}>{device.name} · {device.ipAddress}</option>
              ))}
            </select>
          </label>
          <label>
            Target IP
            <input
              required
              value={connection.ipAddress}
              onChange={(event) => setConnection({ ...connection, ipAddress: event.target.value })}
              placeholder="192.168.1.1"
              disabled={isBusy}
              spellCheck="false"
            />
          </label>
          {credentialSource === 'manual' && (
            <label>
              Community
              <input
                required
                type="password"
                value={connection.community}
                onChange={(event) => setConnection({ ...connection, community: event.target.value })}
                placeholder="Enter community"
                disabled={isBusy}
                autoComplete="new-password"
              />
            </label>
          )}
          <label>
            Timeout (ms)
            <input
              required
              type="number"
              min={500}
              max={10000}
              step={100}
              value={connection.timeoutMilliseconds}
              onChange={(event) => setConnection({ ...connection, timeoutMilliseconds: event.target.value })}
              disabled={isBusy}
            />
          </label>
        </div>

        {deviceLoadError && <div className="inline-alert" role="status">Inventory unavailable: {deviceLoadError}. Manual target entry is still available.</div>}
        <div className="snmp-security-note">
          <ShieldCheck size={17} aria-hidden="true" />
          <span><strong>Security note:</strong> Query only SNMP devices you own or are authorized to manage.</span>
        </div>
      </section>

      <nav className="snmp-tabs" aria-label="SNMP Explorer sections">
        <button className={activeTab === 'overview' ? 'active' : ''} type="button" disabled={isBusy} onClick={() => { setActiveTab('overview'); setError(null) }}>
          <DatabaseZap size={16} /> Overview
        </button>
        <button className={activeTab === 'interfaces' ? 'active' : ''} type="button" disabled={isBusy} onClick={() => { setActiveTab('interfaces'); setError(null) }}>
          <Cable size={16} /> Interfaces
        </button>
        <button className={activeTab === 'custom' ? 'active' : ''} type="button" disabled={isBusy} onClick={() => { setActiveTab('custom'); setError(null) }}>
          <Braces size={16} /> Custom Query
        </button>
      </nav>

      {error && (
        <section className="panel">
          <StatePanel type="error" title="SNMP query failed" message={error} />
        </section>
      )}

      {activeTab === 'overview' && (
        <section className="panel">
          <header className="panel-header">
            <div>
              <h2>System information</h2>
              <p>Standard SNMPv2-MIB system identity and uptime values.</p>
            </div>
            <QueryButton label="Query device" busy={operation === 'system'} disabled={isBusy} onClick={querySystemInfo} />
          </header>
          {operation === 'system' ? (
            <LoadingQuery onCancel={() => requestController.current?.abort()} />
          ) : systemInfo ? (
            <div className="snmp-info-grid">
              <InfoItem label="System Name" value={systemInfo.sysName} />
              <InfoItem label="Object ID" value={systemInfo.sysObjectId} />
              <InfoItem label="Uptime" value={formatUptime(systemInfo.sysUpTimeTicks)} />
              <InfoItem label="Location" value={systemInfo.sysLocation} />
              <InfoItem label="Contact" value={systemInfo.sysContact} />
              <InfoItem label="Description" value={systemInfo.sysDescription} />
            </div>
          ) : (
            <StatePanel type="empty" title="No system data" message="Enter connection details and query the device to read its system OIDs." />
          )}
        </section>
      )}

      {activeTab === 'interfaces' && (
        <section className="panel">
          <header className="panel-header">
            <div>
              <h2>IF-MIB interfaces</h2>
              <p>Administrative and operational state with reported interface speed.</p>
            </div>
            <QueryButton label="Load interfaces" busy={operation === 'interfaces'} disabled={isBusy} onClick={queryInterfaces} />
          </header>
          {operation === 'interfaces' ? (
            <LoadingQuery onCancel={() => requestController.current?.abort()} />
          ) : interfaces === null ? (
            <StatePanel type="empty" title="Interfaces not loaded" message="Query IF-MIB to inspect the target's interface table." />
          ) : interfaces.length === 0 ? (
            <StatePanel type="empty" title="No interfaces returned" message="The SNMP agent did not expose any IF-MIB interface rows." />
          ) : (
            <div className="table-scroll">
              <table className="data-table snmp-interface-table">
                <thead><tr><th>Index</th><th>Interface</th><th>Admin</th><th>Operational</th><th>Speed</th></tr></thead>
                <tbody>
                  {interfaces.map((item) => (
                    <tr key={item.index}>
                      <td className="mono">{item.index}</td>
                      <td>{item.description || 'Unavailable'}</td>
                      <td><SnmpStatus value={item.adminStatus} /></td>
                      <td><SnmpStatus value={item.operStatus} /></td>
                      <td className="mono">{formatSpeed(item.speedBitsPerSecond)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {activeTab === 'custom' && (
        <section className="panel">
          <header className="panel-header">
            <div>
              <h2>Custom query</h2>
              <p>Run a single GET or a server-limited WALK of up to 500 values.</p>
            </div>
          </header>
          <div className="snmp-query-form">
            <div className="query-mode-switch" aria-label="Query mode">
              <button type="button" className={queryMode === 'get' ? 'active' : ''} onClick={() => setQueryMode('get')} disabled={isBusy}>GET</button>
              <button type="button" className={queryMode === 'walk' ? 'active' : ''} onClick={() => setQueryMode('walk')} disabled={isBusy}>WALK</button>
            </div>
            <label>
              {queryMode === 'get' ? 'OID' : 'Root OID'}
              <input value={oid} onChange={(event) => setOid(event.target.value)} disabled={isBusy} spellCheck="false" />
            </label>
            <QueryButton label={`Run ${queryMode.toUpperCase()}`} busy={operation === 'query'} disabled={isBusy} onClick={runCustomQuery} />
          </div>

          {operation === 'query' ? (
            <LoadingQuery onCancel={() => requestController.current?.abort()} />
          ) : getResult ? (
            <div className="snmp-single-result">
              <span className="mono">{getResult.oid}</span>
              <small>{getResult.type}</small>
              <strong>{getResult.value ?? 'No value returned'}</strong>
            </div>
          ) : walkResult ? (
            walkResult.results.length === 0 ? (
              <StatePanel type="empty" title="Empty WALK result" message="The agent returned no values below this OID." />
            ) : (
              <div className="snmp-walk-results">
                <div className="snmp-result-summary"><span>{walkResult.rootOid}</span><strong>{walkResult.count} values</strong></div>
                <div className="table-scroll snmp-walk-scroll">
                  <table className="data-table compact-table">
                    <thead><tr><th>OID</th><th>Type</th><th>Value</th></tr></thead>
                    <tbody>{walkResult.results.map((item) => (
                      <tr key={item.oid}><td className="mono">{item.oid}</td><td>{item.type}</td><td>{item.value ?? '—'}</td></tr>
                    ))}</tbody>
                  </table>
                </div>
              </div>
            )
          ) : (
            <StatePanel type="empty" title="No custom query result" message="Choose GET or WALK, enter an OID, and run the query." />
          )}
        </section>
      )}
    </div>
  )
}

function QueryButton({
  label,
  busy,
  disabled,
  onClick,
}: {
  label: string
  busy: boolean
  disabled: boolean
  onClick: () => void
}) {
  return (
    <button className="button primary" type="button" disabled={disabled} onClick={onClick}>
      {busy ? <LoaderCircle className="spin" size={16} /> : <Play size={15} />}
      {busy ? 'Querying…' : label}
    </button>
  )
}

function LoadingQuery({ onCancel }: { onCancel: () => void }) {
  return (
    <StatePanel
      type="loading"
      title="Waiting for SNMP response"
      message="The read-only SNMP v2c request is in progress…"
      action={<button className="button secondary" type="button" onClick={onCancel}><X size={15} /> Cancel</button>}
    />
  )
}

function SnmpStatus({ value }: { value: SnmpInterface['adminStatus'] }) {
  return <span className={`snmp-status status-${value.toLowerCase()}`}>{value}</span>
}
