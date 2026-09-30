import { request } from './client'
import type { AnalysisStatus } from './resumes'
export type GitHubStatus = { connected: boolean; login: string | null; targetLogin: string | null; status: string | null; connectedAt: string | null }
export type Repository = { id: string; gitHubId: number; fullName: string; private: boolean; defaultBranch: string; htmlUrl: string; includedForAnalysis: boolean; scanStatus: string; analysisJobId: string | null; lastRevisionSha: string | null; lastScanAt: string | null; coverageJson: string }
export type GitHubEvidence = { id: string; repositoryId: string; repository: string; skillId: string | null; originalTerm: string; evidenceType: string; detail: string; strength: string; extractionConfidence: number; lifecycle: string; revisionSha: string; sourcePath: string | null; detector: string; observedAt: string }
export const github = {
  status: () => request<GitHubStatus>('/api/v1/github/status'), connect: () => request<{ url: string }>('/api/v1/github/connect', 'POST', {}),
  disconnect: () => request<void>('/api/v1/github/disconnect', 'POST', {}), repositories: (sync = false) => request<Repository[]>(`/api/v1/github/repositories${sync ? '?sync=true' : ''}`),
  select: (repositoryIds: number[]) => request<void>('/api/v1/github/repositories/selection', 'PUT', { repositoryIds }), scan: () => request<{ analysisJobIds: string[] }>('/api/v1/github/scans', 'POST', {}),
  evidence: () => request<GitHubEvidence[]>('/api/v1/github/evidence'), job: (id: string) => request<AnalysisStatus>(`/api/v1/analysis-jobs/${id}`), retry: (id: string) => request<void>(`/api/v1/analysis-jobs/${id}/retry`, 'POST'),
}
