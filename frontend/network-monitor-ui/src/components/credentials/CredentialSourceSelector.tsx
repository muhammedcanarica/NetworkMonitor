import { useEffect, useState } from 'react'
import { credentialsApi } from '../../api/credentials'
import type { NetworkCredential, NetworkCredentialType } from '../../types/api'

export type CredentialSource = 'manual' | 'saved'

interface CredentialSourceSelectorProps {
  type: NetworkCredentialType
  source: CredentialSource
  credentialId: number | null
  onSourceChange: (source: CredentialSource) => void
  onCredentialChange: (credentialId: number | null) => void
  disabled?: boolean
}

export function CredentialSourceSelector({
  type,
  source,
  credentialId,
  onSourceChange,
  onCredentialChange,
  disabled = false,
}: CredentialSourceSelectorProps) {
  const [credentials, setCredentials] = useState<NetworkCredential[]>([])
  const [loadError, setLoadError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    credentialsApi.list(controller.signal)
      .then((items) => setCredentials(items.filter((item) => item.type === type)))
      .catch((error: unknown) => {
        if (!(error instanceof DOMException && error.name === 'AbortError')) {
          setLoadError(error instanceof Error ? error.message : 'Saved credentials are unavailable.')
        }
      })
    return () => controller.abort()
  }, [type])

  const changeSource = (nextSource: CredentialSource) => {
    onSourceChange(nextSource)
    onCredentialChange(null)
  }

  return (
    <div className="credential-source-field">
      <span>Credential source</span>
      <div className="query-mode-switch" aria-label="Credential source">
        <button type="button" className={source === 'manual' ? 'active' : ''} onClick={() => changeSource('manual')} disabled={disabled}>Manual</button>
        <button type="button" className={source === 'saved' ? 'active' : ''} onClick={() => changeSource('saved')} disabled={disabled}>Saved</button>
      </div>
      {source === 'saved' && (
        <select
          aria-label="Saved credential"
          value={credentialId ?? ''}
          onChange={(event) => onCredentialChange(event.target.value ? Number(event.target.value) : null)}
          disabled={disabled}
        >
          <option value="">Select a saved credential…</option>
          {credentials.map((credential) => (
            <option key={credential.id} value={credential.id}>
              {credential.name}{credential.username ? ` · ${credential.username}` : ''}
            </option>
          ))}
        </select>
      )}
      {source === 'saved' && credentials.length === 0 && !loadError && <small>No compatible saved credentials.</small>}
      {loadError && <small className="field-error">{loadError} Manual entry is still available.</small>}
    </div>
  )
}
