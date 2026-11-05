import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { LandingPage } from './LandingPage'
vi.mock('../components/ProgressPath', () => ({ ProgressPath: () => <div aria-label="Recorrido" /> }))

describe('LandingPage', () => {
  it('explains the product and provides clear entry points', () => {
    render(<LandingPage />)
    expect(screen.getByRole('heading', { name: /Tu trabajo\s*habla por ti\./ })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Analizar mi perfil/ })).toHaveAttribute('href', '/signup')
    expect(screen.getByRole('link', { name: /aviso de privacidad/ })).toHaveAttribute('href', '/privacy')
  })
})
