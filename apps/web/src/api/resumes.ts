import { request } from './client'
export type FactDraft = { kind: 'Experience' | 'Education' | 'Project' | 'Credential'; name: string; organization: string | null; detail: string | null; startDateText: string | null; endDateText: string | null; status: string | null; sourceBlockId: string; quote: string }
export type SkillDraft = { term: string; context: string; sourceBlockId: string; quote: string }
export type BehaviorDraft = { themeKey: string; statement: string; sourceBlockId: string; quote: string }
export type ResumeDraft = { facts: FactDraft[]; skills: SkillDraft[]; behaviors: BehaviorDraft[] }
export type SourceBlock = { id: string; page: number | null; text: string }
export type ResumeSummary = { id: string; version: number; fileName: string; length: number; status: string; analysisJobId: string | null; createdAt: string; confirmedAt: string | null; active: boolean }
export type ResumeReview = { resumeId: string; revision: number; machine: { source: { blocks: SourceBlock[]; warnings: string[] }; draft: ResumeDraft }; draft: ResumeDraft; confirmedAt: string | null; active: boolean }
export type AnalysisStatus = { id: string; state: 'Pending' | 'Processing' | 'Completed' | 'PartiallyCompleted' | 'Failed' | 'Cancelled'; attempts: number; errorCode: string | null; retryable: boolean }
export const resumes = {
  list: () => request<ResumeSummary[]>('/api/v1/resumes/'),
  upload: (file: File) => { const form = new FormData(); form.append('file', file); return request<{ id: string; analysisJobId: string }>('/api/v1/resumes/', 'POST', form) },
  review: (id: string) => request<ResumeReview>(`/api/v1/resumes/${id}/extraction`),
  save: (id: string, revision: number, draft: ResumeDraft) => request<ResumeReview>(`/api/v1/resumes/${id}/extraction`, 'PUT', { revision, draft }),
  confirm: (id: string, revision: number) => request<void>(`/api/v1/resumes/${id}/confirm`, 'POST', { revision }),
  status: (id: string) => request<AnalysisStatus>(`/api/v1/analysis-jobs/${id}`),
  retry: (id: string) => request<void>(`/api/v1/analysis-jobs/${id}/retry`, 'POST'),
  cancel: (id: string) => request<void>(`/api/v1/analysis-jobs/${id}/cancel`, 'POST'),
  downloadUrl: (id: string) => `${import.meta.env.VITE_API_URL ?? ''}/api/v1/resumes/${id}/download`,
}
