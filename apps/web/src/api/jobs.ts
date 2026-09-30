import { ApiError, request } from './client'
import type { AnalysisStatus } from './resumes'

export type RequirementCategory = 'TechnicalSkill' | 'Experience' | 'EducationCredential' | 'Behavioral' | 'Contextual'
export type RequirementLevel = 'Required' | 'Preferred' | 'Unspecified'
export type RequirementImportance = 'Critical' | 'High' | 'Medium' | 'Low'
export type RequirementState = 'Extracted' | 'Excluded'
export type RequirementGroupType = 'None' | 'AnyOf' | 'AllOf'
export type NormalizationStatus = 'NotApplicable' | 'Exact' | 'Alias' | 'Equivalent' | 'Suggested' | 'UserConfirmed' | 'Unresolved'
export type RequirementDraft = {
  key: string; category: RequirementCategory; level: RequirementLevel; importance: RequirementImportance
  state: RequirementState; originalWording: string; skillTerm: string | null; skillId: string | null
  normalizationStatus: NormalizationStatus; behavioralThemeKey: string | null; qualifiers: string[]
  groupKey: string | null; groupType: RequirementGroupType; sourceBlockId: string; quote: string
}
export type RequirementDraftSet = { requirements: RequirementDraft[] }
export type JobSummary = {
  id: string; company: string | null; title: string | null; description: string; sourceUrl: string | null
  descriptionVersion: number; status: 'Processing' | 'ReadyForReview' | 'Confirmed' | 'Failed'
  analysisJobId: string | null; createdAt: string; updatedAt: string; confirmedRequirementSetVersion: number | null
}
export type JobReview = {
  jobId: string; descriptionVersion: number; revision: number
  machine: { source: { blocks: { id: string; text: string }[]; warnings: string[] }; draft: RequirementDraftSet }
  draft: RequirementDraftSet; outdated: boolean; confirmedAt: string | null
}
export type JobInput = { company: string | null; title: string | null; description: string; sourceUrl: string | null; descriptionVersion?: number }
export type SkillOption = { id: string; displayName: string }
export type JobCreateResult = { job: JobSummary; analysisJobId: string }

export type MatchEvidence = { evidenceId: string; sourceEntityId: string; evidenceType: string; strength: string; lifecycle: string; quote: string; sourceReference: string; contribution: number }
export type RequirementMatch = { requirementId: string; key: string; originalWording: string; category: RequirementCategory; level: RequirementLevel; importance: RequirementImportance; evaluationStatus: 'Evaluated' | 'Uncertain' | 'NotEvaluated'; classification: 'Strong' | 'Moderate' | 'Weak' | 'Missing' | null; score: number | null; confidence: number; confidenceBand: 'High' | 'Medium' | 'Low'; relation: string; reasonCode: string; details: string; isStrength: boolean; gapType: string | null; priority: string | null; evidence: MatchEvidence[] }
export type MatchComponent = { name: string; status: 'Applicable' | 'NotApplicable' | 'InsufficientInformation'; score: number | null; coverage: number; confidence: number; appliedWeight: number }
export type MatchGap = { requirementId: string; requirement: string; type: string; priority: string; reason: string }
export type MatchDraft = { scoringVersion: string; overallScore: number | null; classification: string | null; status: 'Complete' | 'CoverageWarning' | 'Limited'; overallConfidence: number; confidenceBand: string; evaluationCoverage: number; requiredCoverage: number | null; components: MatchComponent[]; requirements: RequirementMatch[]; gaps: MatchGap[]; behavioralAssessment: { themeKey: string; observed: boolean; summary: string; evidenceIds: string[] }[]; safeguards: string[] }
export type MatchView = { id: string; jobId: string; requirementSetId: string; scoringVersion: string; createdAt: string; result: MatchDraft }
export type MatchSummary = { id: string; createdAt: string; scoringVersion: string; overallScore: number | null; classification: string | null; status: string; evaluationCoverage: number }
export const jobs = {
  list: () => request<JobSummary[]>('/api/v1/jobs/'),
  get: (id: string) => request<JobSummary>('/api/v1/jobs/' + id),
  create: (input: JobInput) => request<JobCreateResult>('/api/v1/jobs/', 'POST', input),
  update: (id: string, input: JobInput) => request<JobCreateResult>('/api/v1/jobs/' + id, 'PUT', input),
  review: (id: string) => request<JobReview>('/api/v1/jobs/' + id + '/requirements'),
  saveReview: (id: string, revision: number, draft: RequirementDraftSet) =>
    request<JobReview>('/api/v1/jobs/' + id + '/requirements', 'PUT', { revision, draft }),
  confirm: (id: string, revision: number) => request<void>('/api/v1/jobs/' + id + '/confirm', 'POST', { revision }),
  skills: () => request<SkillOption[]>('/api/v1/jobs/skills'),
  status: (id: string) => request<AnalysisStatus>('/api/v1/analysis-jobs/' + id),
  retry: (id: string) => request<void>('/api/v1/analysis-jobs/' + id + '/retry', 'POST'),
  calculateMatch: (id: string) => request<MatchView>('/api/v1/jobs/' + id + '/matches/', 'POST', {}),
  matches: (id: string) => request<MatchSummary[]>('/api/v1/jobs/' + id + '/matches/'),
  latestMatch: async (id: string) => { try { return await request<MatchView>('/api/v1/jobs/' + id + '/matches/latest') } catch (error) { if (error instanceof ApiError && error.status === 404) return null; throw error } },
}
