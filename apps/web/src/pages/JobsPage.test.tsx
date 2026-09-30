import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { JobsPage, ReviewEditor } from './JobsPage'
import { jobs } from '../api/jobs'
import type { JobReview, MatchView } from '../api/jobs'

vi.mock('../api/jobs', () => ({ jobs: {
  list: vi.fn(), create: vi.fn(), update: vi.fn(), review: vi.fn(), saveReview: vi.fn(),
  confirm: vi.fn(), skills: vi.fn(), status: vi.fn(), retry: vi.fn(), calculateMatch: vi.fn(), matches: vi.fn(), latestMatch: vi.fn(),
} }))
function mount(ui: React.ReactNode) {
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{ui}</QueryClientProvider>)
}
const review: JobReview = {
  jobId: 'job-1', descriptionVersion: 1, revision: 1, outdated: false, confirmedAt: null,
  machine: { source: { blocks: [{ id: 'jd-1', text: 'We require React or Vue.' }], warnings: [] }, draft: { requirements: [] } },
  draft: { requirements: [{
    key: 'req-1', category: 'TechnicalSkill', level: 'Required', importance: 'High', state: 'Extracted',
    originalWording: 'Vue', skillTerm: 'Vue', skillId: null, normalizationStatus: 'Unresolved',
    behavioralThemeKey: null, qualifiers: [], groupKey: 'frontend', groupType: 'AnyOf',
    sourceBlockId: 'jd-1', quote: 'We require React or Vue.',
  }] },
}
beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(jobs.skills).mockResolvedValue([{ id: 'react', displayName: 'React' }])
  vi.mocked(jobs.matches).mockResolvedValue([])
  vi.mocked(jobs.latestMatch).mockResolvedValue(null)
})
describe('job intelligence', () => {
  it('keeps the description after a failed create request', async () => {
    vi.mocked(jobs.list).mockResolvedValue([])
    vi.mocked(jobs.create).mockRejectedValueOnce(new Error('Provider unavailable'))
    mount(<JobsPage />)
    const description = 'A'.repeat(120)
    await userEvent.setup().type(await screen.findByLabelText(/Job description/), description)
    await userEvent.setup().click(screen.getByRole('button', { name: 'Save and analyze' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Provider unavailable')
    expect(screen.getByLabelText(/Job description/)).toHaveValue(description)
  })
  it('shows unresolved alternatives and requires review before confirmation', async () => {
    mount(<ReviewEditor review={review} />)
    expect(await screen.findByText('Unresolved · not evaluated')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Confirm requirement set' })).toBeDisabled()
    await screen.findByRole('option', { name: 'React' })
    await userEvent.setup().selectOptions(screen.getByLabelText('Canonical skill'), 'react')
    expect(screen.getByRole('checkbox')).not.toBeChecked()
    await userEvent.setup().click(screen.getByRole('checkbox'))
    expect(screen.getByRole('button', { name: 'Confirm requirement set' })).toBeEnabled()
  })
  it('locks a confirmed requirement set', async () => {
    mount(<ReviewEditor review={{ ...review, confirmedAt: '2026-09-29T00:00:00Z' }} />)
    expect(await screen.findByRole('status')).toHaveTextContent('immutable')
    expect(screen.getByLabelText('Canonical skill')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Confirm requirement set' })).not.toBeInTheDocument()
  })
  it('shows traceable matching results for a confirmed job', async () => {
    vi.mocked(jobs.list).mockResolvedValue([{ id: 'job-1', company: 'Acme', title: 'Engineer', description: 'A'.repeat(120), sourceUrl: null, descriptionVersion: 1, status: 'Confirmed', analysisJobId: null, createdAt: '2026-09-29T00:00:00Z', updatedAt: '2026-09-29T00:00:00Z', confirmedRequirementSetVersion: 1 }])
    vi.mocked(jobs.review).mockResolvedValue({ ...review, confirmedAt: '2026-09-29T00:00:00Z' })
    const match: MatchView = { id: '11111111-1111-1111-1111-111111111111', jobId: 'job-1', requirementSetId: 'set-1', scoringVersion: 'matching-v1', createdAt: '2026-09-29T00:00:00Z', result: { scoringVersion: 'matching-v1', overallScore: 86, classification: 'StrongMatch', status: 'Complete', overallConfidence: .9, confidenceBand: 'High', evaluationCoverage: 1, requiredCoverage: 1, safeguards: [], components: [{ name: 'Technical', status: 'Applicable', score: 90, coverage: 1, confidence: .9, appliedWeight: .45 }], requirements: [{ requirementId: 'req-1', key: 'csharp', originalWording: 'C#', category: 'TechnicalSkill', level: 'Required', importance: 'High', evaluationStatus: 'Evaluated', classification: 'Strong', score: 100, confidence: .9, confidenceBand: 'High', relation: 'ExactCanonical', reasonCode: 'SUPPORTED', details: 'Canonical evidence supports this requirement.', isStrength: true, gapType: null, priority: null, evidence: [{ evidenceId: 'ev-1', sourceEntityId: 'resume-1', evidenceType: 'Implementation', strength: 'Strong', lifecycle: 'Active', quote: 'Built an API in C#', sourceReference: 'resume-1', contribution: 1 }] }], gaps: [], behavioralAssessment: [] } }
    vi.mocked(jobs.latestMatch).mockResolvedValue(match)
    vi.mocked(jobs.matches).mockResolvedValue([{ id: match.id, createdAt: match.createdAt, scoringVersion: 'matching-v1', overallScore: 86, classification: 'StrongMatch', status: 'Complete', evaluationCoverage: 1 }])
    mount(<JobsPage />)
    await userEvent.setup().click(await screen.findByRole('button', { name: /Engineer/ }))
    expect(await screen.findByText('86%')).toBeInTheDocument()
    expect(screen.getByText('matching-v1')).toBeInTheDocument()
    await userEvent.setup().click(screen.getByText('C#'))
    expect(await screen.findByText(/Built an API in C#/)).toBeInTheDocument()
  })})
