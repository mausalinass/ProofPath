import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { it, expect, vi } from 'vitest'
import { SettingsPage } from './SettingsPage'
import { api } from '../api/client'
vi.mock('../api/client', () => ({ api: { deleteAccount: vi.fn() } }))

it('requires explicit deletion confirmation and can cancel without deleting', async () => {
  const client = new QueryClient(); const user = userEvent.setup()
  render(<QueryClientProvider client={client}><SettingsPage session={{ id: 'test', email: 'candidate@example.test' }} /></QueryClientProvider>)
  expect(screen.queryByLabelText('Current password')).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Delete my account' }))
  expect(screen.getByLabelText('Current password')).toBeRequired()
  await user.click(screen.getByRole('button', { name: 'Cancel' }))
  expect(api.deleteAccount).not.toHaveBeenCalled()
  expect(screen.queryByLabelText('Current password')).not.toBeInTheDocument()
})
