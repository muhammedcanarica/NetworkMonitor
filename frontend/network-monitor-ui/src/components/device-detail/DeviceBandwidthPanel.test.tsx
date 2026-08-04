import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { credentialsApi } from '../../api/credentials'
import { snmpMonitoringApi } from '../../api/snmpMonitoring'
import type { InterfaceTrafficSummary, SnmpMonitoringProfile } from '../../types/api'
import { DeviceBandwidthPanel } from './DeviceBandwidthPanel'

vi.mock('../../api/credentials', () => ({ credentialsApi: { list: vi.fn() } }))
vi.mock('../../api/snmpMonitoring', () => ({ snmpMonitoringApi: {
  get: vi.fn(), summary: vi.fn(), history: vi.fn(), discoverInterfaces: vi.fn(), update: vi.fn(),
  disable: vi.fn(), updateThreshold: vi.fn(), deleteThreshold: vi.fn(),
} }))
vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => children,
  LineChart: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  CartesianGrid: () => null, Line: () => null, Tooltip: () => null, XAxis: () => null, YAxis: () => null,
}))

const profile: SnmpMonitoringProfile = {
  deviceId: 1, credentialId: 3, isEnabled: true,
  createdAt: '2026-08-04T08:00:00Z', updatedAt: '2026-08-04T08:00:00Z', interfaces: [],
}

function summary(overrides: Partial<InterfaceTrafficSummary> = {}): InterfaceTrafficSummary {
  return {
    interfaceIndex: 1, interfaceName: 'uplink0', description: null, adminStatus: 'Up', operStatus: 'Up',
    lastSampleAt: '2026-08-04T08:01:00Z', inboundBitsPerSecond: null, outboundBitsPerSecond: null,
    threshold: null, hasOpenInboundAlert: false, hasOpenOutboundAlert: false, hasActiveDownIncident: false,
    ...overrides,
  }
}

describe('DeviceBandwidthPanel', () => {
  beforeEach(() => {
    vi.mocked(credentialsApi.list).mockResolvedValue([])
    vi.mocked(snmpMonitoringApi.get).mockResolvedValue(profile)
    vi.mocked(snmpMonitoringApi.summary).mockResolvedValue([])
    vi.mocked(snmpMonitoringApi.history).mockResolvedValue({ interfaceIndex: 1, interfaceName: 'uplink0', hours: 1, samples: [] })
  })

  it('shows setup when monitoring has not been configured', async () => {
    vi.mocked(snmpMonitoringApi.get).mockResolvedValue(null)
    render(<DeviceBandwidthPanel deviceId={1} />)
    expect(await screen.findByText('Saved SNMP credential')).toBeInTheDocument()
  })

  it('shows Collecting baseline instead of 0 Mbps for missing rates', async () => {
    vi.mocked(snmpMonitoringApi.summary).mockResolvedValue([summary()])
    render(<DeviceBandwidthPanel deviceId={1} />)
    expect((await screen.findAllByText('Collecting baseline')).length).toBeGreaterThanOrEqual(2)
    expect(screen.queryByText('0 Mbps')).not.toBeInTheDocument()
  })

  it('shows an active bandwidth incident', async () => {
    vi.mocked(snmpMonitoringApi.summary).mockResolvedValue([summary({ hasOpenInboundAlert: true, inboundBitsPerSecond: 2_000_000 })])
    render(<DeviceBandwidthPanel deviceId={1} />)
    expect(await screen.findByText('Inbound alert')).toBeInTheDocument()
  })

  it('does not infer an incident badge from Down status alone', async () => {
    vi.mocked(snmpMonitoringApi.summary).mockResolvedValue([summary({ operStatus: 'Down' })])
    render(<DeviceBandwidthPanel deviceId={1} />)
    expect(await screen.findByText('Down')).toBeInTheDocument()
    expect(screen.queryByText('Interface Down')).not.toBeInTheDocument()
  })
})
