import { render, screen } from '@testing-library/react'
import { it, expect } from 'vitest'
import { PrivacyPage } from './PrivacyPage'

it('publishes provider retention and account deletion choices', () => {
  render(<PrivacyPage />)
  expect(screen.getByRole('heading', { name: 'Your evidence remains yours.' })).toBeInTheDocument()
  expect(screen.getByText(/up to 30 days/)).toBeInTheDocument()
  expect(screen.getByText(/Deleting the account removes database records/)).toBeInTheDocument()
})
