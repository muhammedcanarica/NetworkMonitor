const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim()

export const API_BASE_URL = (configuredBaseUrl || 'http://localhost:5107').replace(
  /\/$/,
  '',
)

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  public readonly status: number
  public readonly fieldErrors: Record<string, string[]>

  constructor(
    message: string,
    status: number,
    fieldErrors: Record<string, string[]> = {},
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null
}

async function parseError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails = {}

  try {
    const body: unknown = await response.json()
    if (isProblemDetails(body)) problem = body
  } catch {
    // Some server errors do not include a JSON response body.
  }

  const validationMessage = Object.values(problem.errors ?? {}).flat()[0]
  const fallback =
    response.status === 409
      ? 'Bu IP adresine sahip bir cihaz zaten kayıtlı.'
      : 'İşlem tamamlanamadı. Lütfen tekrar deneyin.'

  return new ApiError(
    problem.detail || validationMessage || problem.title || fallback,
    response.status,
    problem.errors,
  )
}

export async function apiRequest<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  try {
    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers: {
        Accept: 'application/json',
        ...(options.body ? { 'Content-Type': 'application/json' } : {}),
        ...options.headers,
      },
    })

    if (!response.ok) throw await parseError(response)
    if (response.status === 204) return undefined as T

    return (await response.json()) as T
  } catch (error) {
    if (
      error instanceof ApiError ||
      (error instanceof DOMException && error.name === 'AbortError')
    ) {
      throw error
    }

    throw new ApiError(
      "Backend'e bağlanılamıyor. API servisinin çalıştığını kontrol edin.",
      0,
    )
  }
}
