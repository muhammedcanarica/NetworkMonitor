import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { credentialsApi } from '../../api/credentials'
import type { NetworkCredential } from '../../types/api'
import { CredentialSourceSelector, type CredentialSource } from './CredentialSourceSelector'

vi.mock('../../api/credentials', () => ({ credentialsApi: { list: vi.fn() } }))

const savedCredential = {
  id: 11, name: 'Lab SSH', type: 'SshPassword', username: 'operator', deviceId: null,
  createdAt: '2026-08-04T08:00:00Z', updatedAt: '2026-08-04T08:00:00Z', hasSecret: true,
  secret: 'must-never-render',
} as NetworkCredential

function Harness() {
  const [source, setSource] = useState<CredentialSource>('manual')
  const [credentialId, setCredentialId] = useState<number | null>(null)
  return <CredentialSourceSelector type="SshPassword" source={source} credentialId={credentialId} onSourceChange={setSource} onCredentialChange={setCredentialId} />
}

describe('CredentialSourceSelector', () => {
  beforeEach(() => vi.mocked(credentialsApi.list).mockResolvedValue([savedCredential]))

  it('switches between Manual and Saved modes', async () => {
    render(<Harness />)
    expect(screen.queryByLabelText('Saved credential')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Saved' }))
    expect(await screen.findByLabelText('Saved credential')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Manual' }))
    expect(screen.queryByLabelText('Saved credential')).not.toBeInTheDocument()
  })

  it('shows only the requested credential type without exposing its secret', async () => {
    render(<Harness />)
    await userEvent.click(screen.getByRole('button', { name: 'Saved' }))
    expect(await screen.findByRole('option', { name: /Lab SSH/ })).toBeInTheDocument()
    expect(screen.queryByText('must-never-render')).not.toBeInTheDocument()
  })

  it('does not automatically select a saved credential', async () => {
    render(<Harness />)
    await userEvent.click(screen.getByRole('button', { name: 'Saved' }))
    expect(await screen.findByLabelText('Saved credential')).toHaveValue('')
  })
})
