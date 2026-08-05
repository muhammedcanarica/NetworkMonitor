import { describe, expect, it, vi } from 'vitest'
import { apiRequest } from './client'
import { snmpMonitoringApi } from './snmpMonitoring'

vi.mock('./client', () => ({ apiRequest: vi.fn() }))

describe('snmpMonitoringApi.get', () => {
  it('normalizes an empty 204 response to null', async () => {
    vi.mocked(apiRequest).mockResolvedValue(undefined)

    await expect(snmpMonitoringApi.get(42)).resolves.toBeNull()
    expect(apiRequest).toHaveBeenCalledWith('/api/devices/42/snmp-monitoring', { signal: undefined })
  })
})
