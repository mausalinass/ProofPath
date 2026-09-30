import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { GitHubPage } from './GitHubPage'
import { github } from '../api/github'

vi.mock('../api/github', () => ({
  github: {
    status: vi.fn(), repositories: vi.fn(), evidence: vi.fn(), connect: vi.fn(),
    select: vi.fn(), scan: vi.fn(), disconnect: vi.fn(), job: vi.fn(), retry: vi.fn(),
  },
}))

function mount() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={client}><GitHubPage /></QueryClientProvider>)
}

beforeEach(() => vi.resetAllMocks())

describe('GitHub evidence workspace', () => {
  it('requires an explicit repository selection and enforces the five repository limit', async () => {
    vi.mocked(github.status).mockResolvedValue({ connected: true, login: 'octocat', targetLogin: 'octocat', status: 'Active', connectedAt: '2026-09-29T00:00:00Z' })
    vi.mocked(github.repositories).mockResolvedValue(Array.from({ length: 6 }, (_, index) => ({
      id: `repository-${index + 1}`, gitHubId: index + 1, fullName: `octocat/repo-${index + 1}`,
      private: false, defaultBranch: 'main', htmlUrl: `https://github.test/octocat/repo-${index + 1}`,
      includedForAnalysis: false, scanStatus: 'NeverScanned', analysisJobId: null,
      lastRevisionSha: null, lastScanAt: null, coverageJson: '{}',
    })))
    vi.mocked(github.evidence).mockResolvedValue([])
    vi.mocked(github.select).mockResolvedValue(undefined)
    const user = userEvent.setup()
    mount()

    const boxes = await screen.findAllByRole('checkbox')
    expect(screen.getByText('0 of 5 selected')).toBeInTheDocument()
    for (const box of boxes.slice(0, 5)) await user.click(box)
    expect(screen.getByText('5 of 5 selected')).toBeInTheDocument()
    expect(boxes[5]).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Save selection' }))
    expect(github.select).toHaveBeenCalledWith([1, 2, 3, 4, 5])
  })
})
