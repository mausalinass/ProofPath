import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ProfilePage } from './ProfilePage'
import { api } from '../api/client'

vi.mock('../api/client', () => ({ api: { profile: vi.fn(), saveProfile: vi.fn() } }))
const profile = { id: 'test', firstName: 'Ada', lastName: null, headline: null, location: null, workAuthorization: null, educationSummary: null, createdAt: '', updatedAt: '' }
function mount() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  render(<QueryClientProvider client={client}><ProfilePage /></QueryClientProvider>)
}
beforeEach(() => vi.resetAllMocks())
describe('profile review', () => {
  it('preserves entered facts after save failure and allows retry', async () => {
    vi.mocked(api.profile).mockResolvedValue(profile)
    vi.mocked(api.saveProfile).mockRejectedValueOnce(new Error('Connection interrupted')).mockResolvedValueOnce({ ...profile, headline: 'Engineer' })
    mount(); const user = userEvent.setup()
    const headline = await screen.findByLabelText('Headline'); await user.type(headline, 'Engineer')
    await user.click(screen.getByRole('button', { name: 'Save profile' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Connection interrupted')
    expect(headline).toHaveValue('Engineer')
    await user.click(screen.getByRole('button', { name: 'Save profile' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Profile saved')
  })
  it('does not show a fake profile when the server has none', async () => {
    vi.mocked(api.profile).mockResolvedValue(null); mount()
    expect(await screen.findByRole('heading', { name: 'Set up your profile' })).toBeVisible()
    expect(screen.getByLabelText('First name')).toHaveValue('')
  })
})
