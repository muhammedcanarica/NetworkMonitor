import { useEffect, useRef, useState } from 'react'
import { LoaderCircle, Power, ShieldCheck, X } from 'lucide-react'
import { wakeOnLanApi } from '../api/wakeOnLan'
import { StatePanel } from '../components/ui/StatePanel'
import type { WakeOnLanRequest } from '../types/api'

interface WakeOnLanForm {
  macAddress: string
  broadcastAddress: string
  port: string
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'The magic packet could not be sent.'
}

function isAbortError(error: unknown) {
  return error instanceof DOMException && error.name === 'AbortError'
}

function isValidMacAddress(value: string) {
  return /^(?:[0-9A-Fa-f]{12}|(?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}|(?:[0-9A-Fa-f]{2}-){5}[0-9A-Fa-f]{2})$/.test(value)
}

function isValidIpv4Address(value: string) {
  const segments = value.split('.')
  return segments.length === 4 && segments.every((segment) => {
    if (!/^\d{1,3}$/.test(segment)) return false
    const number = Number(segment)
    return number >= 0 && number <= 255
  })
}

export function WakeOnLanPage() {
  const [form, setForm] = useState<WakeOnLanForm>({
    macAddress: '',
    broadcastAddress: '255.255.255.255',
    port: '9',
  })
  const [isSending, setIsSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const requestController = useRef<AbortController | null>(null)

  useEffect(() => () => requestController.current?.abort(), [])

  const updateField = (field: keyof WakeOnLanForm, value: string) => {
    setForm((current) => ({ ...current, [field]: value }))
  }

  const createRequest = (): WakeOnLanRequest | null => {
    const macAddress = form.macAddress.trim()
    const broadcastAddress = form.broadcastAddress.trim()
    const port = Number(form.port)

    if (!isValidMacAddress(macAddress)) {
      setError('Enter a valid MAC address, such as 00:11:22:33:44:55.')
      return null
    }
    if (!isValidIpv4Address(broadcastAddress) || broadcastAddress === '0.0.0.0') {
      setError('Enter a valid IPv4 broadcast address.')
      return null
    }
    if (!Number.isInteger(port) || port < 1 || port > 65535) {
      setError('UDP port must be between 1 and 65535.')
      return null
    }

    return { macAddress, broadcastAddress, port }
  }

  const sendMagicPacket = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setNotice(null)
    const request = createRequest()
    if (!request) return

    const controller = new AbortController()
    requestController.current = controller
    setIsSending(true)
    setError(null)
    setNotice(null)

    try {
      const response = await wakeOnLanApi.send(request, controller.signal)
      setNotice(response.message)
    } catch (requestError) {
      if (isAbortError(requestError)) setNotice('Wake-on-LAN request cancelled.')
      else setError(getErrorMessage(requestError))
    } finally {
      if (requestController.current === controller) requestController.current = null
      setIsSending(false)
    }
  }

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Remote power management</span>
          <h1>Wake-on-LAN</h1>
          <p>Send a standard UDP magic packet to a device on your authorized network.</p>
        </div>
      </header>

      {notice && <div className="success-alert" role="status">{notice}</div>}

      <section className="panel wol-control-panel">
        <header className="panel-header">
          <div>
            <h2>Send magic packet</h2>
            <p>The target must support Wake-on-LAN and be reachable through the selected broadcast network.</p>
          </div>
          <Power size={22} aria-hidden="true" />
        </header>

        <form className="wol-form" onSubmit={sendMagicPacket}>
          <label>
            Target MAC address
            <input
              value={form.macAddress}
              onChange={(event) => updateField('macAddress', event.target.value)}
              placeholder="00:11:22:33:44:55"
              disabled={isSending}
              spellCheck="false"
              autoComplete="off"
            />
          </label>
          <label>
            IPv4 broadcast address
            <input
              value={form.broadcastAddress}
              onChange={(event) => updateField('broadcastAddress', event.target.value)}
              placeholder="192.168.1.255"
              disabled={isSending}
              spellCheck="false"
              autoComplete="off"
            />
          </label>
          <label>
            UDP port
            <input
              type="number"
              min={1}
              max={65535}
              step={1}
              value={form.port}
              onChange={(event) => updateField('port', event.target.value)}
              disabled={isSending}
            />
          </label>
          <div className="wol-submit">
            <button className="button primary" type="submit" disabled={isSending}>
              {isSending ? <LoaderCircle className="spin" size={16} /> : <Power size={16} />}
              {isSending ? 'Sending…' : 'Send magic packet'}
            </button>
          </div>
        </form>

        <div className="wol-security-note">
          <ShieldCheck size={17} aria-hidden="true" />
          <span><strong>Important:</strong> Sending a magic packet does not confirm that the target powered on. Use this tool only for devices and networks you are authorized to manage.</span>
        </div>
      </section>

      {error && (
        <section className="panel">
          <StatePanel type="error" title="Magic packet could not be sent" message={error} />
        </section>
      )}

      {isSending && (
        <section className="panel">
          <StatePanel
            type="loading"
            title="Sending magic packet"
            message="The Wake-on-LAN broadcast is being sent…"
            action={<button className="button secondary" type="button" onClick={() => requestController.current?.abort()}><X size={15} /> Cancel</button>}
          />
        </section>
      )}
    </div>
  )
}
