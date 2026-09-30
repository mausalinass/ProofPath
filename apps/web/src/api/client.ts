export type Session = { id: string; email: string }
export type ProfileInput = {
  firstName: string | null; lastName: string | null; headline: string | null
  location: string | null; workAuthorization: string | null; educationSummary: string | null
}
export type Profile = ProfileInput & { id: string; createdAt: string; updatedAt: string }
export type Home = { profile: Profile | null; profileComplete: boolean; nextAction: string }

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) { super(message); this.status = status }
}

const base = import.meta.env.VITE_API_URL ?? ''
export async function request<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}
  if (method !== 'GET') {
    const tokenResponse = await fetch(`${base}/api/v1/auth/csrf`, { credentials: 'include', cache: 'no-store' })
    if (!tokenResponse.ok) throw new ApiError(tokenResponse.status, 'Could not secure the form. Please retry.')
    const { token } = await tokenResponse.json() as { token: string }
    headers['X-CSRF-TOKEN'] = token
    if (!(body instanceof FormData)) headers['Content-Type'] = 'application/json'
  }
  const response = await fetch(`${base}${path}`, {
    method, headers, credentials: 'include', cache: 'no-store',
    body: body === undefined ? undefined : body instanceof FormData ? body : JSON.stringify(body),
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => ({})) as { detail?: string; title?: string; errors?: Record<string, string[]> }
    const message = problem.errors ? Object.values(problem.errors).flat().join(' ') : problem.detail ?? problem.title ?? 'Request failed. Please retry.'
    throw new ApiError(response.status, message)
  }
  return response.status === 204 ? undefined as T : await response.json() as T
}

export const api = {
  session: async () => {
    try { return await request<Session>('/api/v1/auth/me') }
    catch (error) { if (error instanceof ApiError && error.status === 401) return null; throw error }
  },
  profile: async () => {
    try { return await request<Profile>('/api/v1/profile') }
    catch (error) { if (error instanceof ApiError && error.status === 404) return null; throw error }
  },
  home: () => request<Home>('/api/v1/home'),
  saveProfile: (profile: ProfileInput) => request<Profile>('/api/v1/profile', 'PUT', profile),
  register: (values: { email: string; password: string; firstName: string; lastName: string }) => request<Session>('/api/v1/auth/register', 'POST', values),
  login: (email: string, password: string) => request<Session>('/api/v1/auth/login', 'POST', { email, password }),
  logout: () => request<void>('/api/v1/auth/logout', 'POST', {}),
  deleteAccount: (password: string) => request<void>('/api/v1/account', 'DELETE', { confirm: true, password }),
}
