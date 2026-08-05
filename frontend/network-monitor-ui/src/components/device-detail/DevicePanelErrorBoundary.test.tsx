import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { DevicePanelErrorBoundary } from './DevicePanelErrorBoundary'

function BrokenPanel(): never {
  throw new Error('Simulated panel render failure')
}

describe('DevicePanelErrorBoundary', () => {
  it('keeps a panel render failure inside the affected panel', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)

    render(<div><span>Other device details</span><DevicePanelErrorBoundary panelName="Interface Traffic"><BrokenPanel /></DevicePanelErrorBoundary></div>)

    expect(screen.getByText('Other device details')).toBeInTheDocument()
    expect(screen.getByText('Interface Traffic unavailable')).toBeInTheDocument()
  })
})
