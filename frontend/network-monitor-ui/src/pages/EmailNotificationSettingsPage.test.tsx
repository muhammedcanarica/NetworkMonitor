import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { emailNotificationsApi } from '../api/emailNotifications'
import type { EmailNotificationSettings } from '../types/api'
import { EmailNotificationSettingsPage } from './EmailNotificationSettingsPage'

vi.mock('../api/emailNotifications', () => ({ emailNotificationsApi: { get: vi.fn(), update: vi.fn(), sendTest: vi.fn() } }))

const settings: EmailNotificationSettings = {
  isEnabled: true, host: 'smtp.example.test', port: 587, tlsMode: 'StartTls', username: 'mailer',
  fromAddress: 'alerts@example.test', fromName: 'NetScope', recipientAddresses: ['admin@example.test'],
  hasPassword: true, updatedAt: '2026-08-04T08:00:00Z',
}

describe('EmailNotificationSettingsPage', () => {
  beforeEach(() => {
    vi.mocked(emailNotificationsApi.get).mockResolvedValue(settings)
    vi.mocked(emailNotificationsApi.update).mockResolvedValue(settings)
    vi.mocked(emailNotificationsApi.sendTest).mockResolvedValue({ message: 'Test accepted.' })
  })

  it('never renders a password returned by the backend and shows configured state', async () => {
    vi.mocked(emailNotificationsApi.get).mockResolvedValue({ ...settings, password: 'server-secret' } as EmailNotificationSettings)
    render(<EmailNotificationSettingsPage />)
    const password = await screen.findByLabelText(/Password/)
    expect(password).toHaveValue('')
    expect(screen.queryByDisplayValue('server-secret')).not.toBeInTheDocument()
    expect(screen.getByText('A protected password is configured.')).toBeInTheDocument()
  })

  it('sends null for a blank password so the existing password is preserved', async () => {
    render(<EmailNotificationSettingsPage />)
    await screen.findByDisplayValue('smtp.example.test')
    await userEvent.click(screen.getByRole('button', { name: /save settings/i }))
    await waitFor(() => expect(emailNotificationsApi.update).toHaveBeenCalledWith(expect.objectContaining({ password: null })))
  })

  it('calls the test email endpoint', async () => {
    render(<EmailNotificationSettingsPage />)
    await screen.findByDisplayValue('smtp.example.test')
    await userEvent.click(screen.getByRole('button', { name: /send test email/i }))
    await waitFor(() => expect(emailNotificationsApi.sendTest).toHaveBeenCalledOnce())
    expect(await screen.findByText('Test accepted.')).toBeInTheDocument()
  })

  it('shows a loading state while settings are requested', () => {
    vi.mocked(emailNotificationsApi.get).mockReturnValue(new Promise(() => undefined))
    render(<EmailNotificationSettingsPage />)
    expect(screen.getByText('Loading email settings')).toBeInTheDocument()
  })

  it('shows a recoverable error state when settings cannot be loaded', async () => {
    vi.mocked(emailNotificationsApi.get).mockRejectedValue(new Error('Settings request failed.'))
    render(<EmailNotificationSettingsPage />)
    expect(await screen.findByText('Email settings unavailable')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })
})
