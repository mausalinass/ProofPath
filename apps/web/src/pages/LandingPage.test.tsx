import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { LandingPage } from './LandingPage'

describe('LandingPage', () => {
  it('explains the product and provides clear entry points', () => {
    render(<LandingPage />)
    expect(screen.getByRole('heading', { name: 'Know what your experience proves.' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Build your evidence profile' })).toHaveAttribute('href', '/signup')
    expect(screen.getByText('Evidence without exposure')).toBeInTheDocument()
  })
})
