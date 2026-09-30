import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ReviewEditor, ResumePage } from './ResumePage'
import { resumes } from '../api/resumes'
import type { ResumeReview } from '../api/resumes'
vi.mock('../api/resumes', () => ({ resumes: { list: vi.fn(), upload: vi.fn(), save: vi.fn(), confirm: vi.fn() } }))
const review: ResumeReview = {
  resumeId: 'resume-test', revision: 1, confirmedAt: null, active: false,
  machine: { source: { blocks: [{ id: 'p1', page: 1, text: 'Engineer at Acme' }], warnings: [] }, draft: { facts: [], skills: [], behaviors: [] } },
  draft: { facts: [{ kind: 'Experience', name: 'Engineer', organization: 'Acme', detail: null, startDateText: null, endDateText: null, status: null, sourceBlockId: 'p1', quote: 'Engineer at Acme' }], skills: [], behaviors: [] },
}
function mount(ui: React.ReactNode) { render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{ui}</QueryClientProvider>) }
beforeEach(() => vi.resetAllMocks())
describe('résumé review', () => {
  it('requires explicit acknowledgement and saves corrections before allowing confirmation', async () => {
    const user = userEvent.setup(); mount(<ReviewEditor review={review} />)
    expect(screen.getByRole('button', { name: 'Confirm résumé facts' })).toBeDisabled()
    await user.click(screen.getByRole('checkbox')); expect(screen.getByRole('button', { name: 'Confirm résumé facts' })).toBeEnabled()
    await user.type(screen.getByLabelText('Name / role / degree'), ' II')
    expect(screen.getByRole('checkbox')).not.toBeChecked(); expect(screen.getByRole('button', { name: 'Confirm résumé facts' })).toBeDisabled()
    vi.mocked(resumes.save).mockRejectedValueOnce(new Error('Temporary failure'))
    await user.click(screen.getByRole('button', { name: 'Save corrections' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Temporary failure')
    expect(screen.getByLabelText('Name / role / degree')).toHaveValue('Engineer II')
    expect(resumes.confirm).not.toHaveBeenCalled()
  })
  it('locks historical confirmed versions and identifies them as inactive', () => {
    mount(<ReviewEditor review={{ ...review, confirmedAt: '2026-09-18T00:00:00Z', active: false }} />)
    expect(screen.getByRole('status')).toHaveTextContent('Historical version')
    expect(screen.getByLabelText('Name / role / degree')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Confirm résumé facts' })).not.toBeInTheDocument()
  })
  it('rejects an unsupported file before sending it', async () => {
    vi.mocked(resumes.list).mockResolvedValue([]); mount(<ResumePage />)
    const input = await screen.findByLabelText('Résumé file')
    await userEvent.setup({ applyAccept: false }).upload(input, new File(['not a resume'], 'resume.exe', { type: 'application/octet-stream' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('PDF or DOCX')
    expect(screen.getByRole('button', { name: 'Upload résumé' })).toBeDisabled(); expect(resumes.upload).not.toHaveBeenCalled()
  })
})
