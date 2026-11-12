import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { github } from '../api/github'
import { ErrorNotice, Loading } from '../components/Feedback'
import './GitHubPage.css'

const terminal = ['Completed', 'PartiallyCompleted', 'Failed', 'Cancelled']
const scanMessages: Record<string, string> = {
  GITHUB_APP_CREDENTIALS: 'The GitHub App could not authenticate. Contact support.',
  GITHUB_ACCESS_LOST: 'Access was lost. Review the GitHub App installation and reconnect if needed.',
  GITHUB_PERMISSIONS_INVALID: 'Grant the GitHub App read access to Contents and Metadata, then try again.',
  GITHUB_REPOSITORY_NOT_GRANTED: 'Grant this repository to the GitHub App, refresh, and try again.',
  GITHUB_REPOSITORY_EMPTY: 'Add an initial commit on GitHub, then select Analyze selected again.',
  GITHUB_BRANCH_MISSING: 'Check that the default branch exists on GitHub, then refresh and analyze again.',
  GITHUB_OBJECT_MISSING: 'GitHub could not find part of this revision. Refresh and analyze again.',
  GITHUB_REPOSITORY_CONFLICT: 'GitHub could not read this repository. Check it on GitHub and analyze again.',
  GITHUB_RATE_LIMITED: 'GitHub is limiting requests. The next attempt will respect GitHub’s wait time.',
  GITHUB_NETWORK_ERROR: 'The connection to GitHub timed out or was interrupted.',
  GITHUB_UNAVAILABLE: 'GitHub returned a temporary server error.',
  GITHUB_INVALID_RESPONSE: 'GitHub returned an unexpected response. Contact support if this persists.',
  GITHUB_REQUEST_REJECTED: 'GitHub rejected the analysis request. Contact support if this persists.',
}
export function GitHubPage() {
  const client = useQueryClient(), [search, setSearch] = useState(''), [selectionOverride, setSelected] = useState<number[]>([]), [selectionTouched, setSelectionTouched] = useState(false), [jobs, setJobs] = useState<string[]>([])
  const status = useQuery({ queryKey: ['github-status'], queryFn: github.status })
  const repos = useQuery({ queryKey: ['github-repositories'], queryFn: () => github.repositories(false), enabled: status.data?.connected === true })
  const evidence = useQuery({ queryKey: ['github-evidence'], queryFn: github.evidence, enabled: status.data?.connected === true })
  const selected = selectionTouched ? selectionOverride : repos.data?.filter(item => item.includedForAnalysis).map(item => item.gitHubId) ?? []
  const progress = useQueries({
    queries: jobs.map(id => ({
      queryKey: ['analysis-job', id],
      queryFn: () => github.job(id),
      refetchInterval: (query: { state: { data?: { state: string } } }) =>
        terminal.includes(query.state.data?.state ?? '') ? false : 1500,
    })),
  })
  const finished = progress.length > 0 && progress.every(item => item.data && terminal.includes(item.data.state))
  useEffect(() => { if (finished) { void client.invalidateQueries({ queryKey: ['github-repositories'] }); void client.invalidateQueries({ queryKey: ['github-evidence'] }) } }, [finished, client])
  const connect = useMutation({ mutationFn: github.connect, onSuccess: result => window.location.assign(result.url) })
  const synchronize = useMutation({ mutationFn: () => github.repositories(true), onSuccess: result => client.setQueryData(['github-repositories'], result) })
  const save = useMutation({ mutationFn: () => github.select(selected), onSuccess: () => client.invalidateQueries({ queryKey: ['github-repositories'] }) })
  const scan = useMutation({ mutationFn: async () => { await github.select(selected); return github.scan() }, onSuccess: result => { setJobs(result.analysisJobIds); void client.invalidateQueries({ queryKey: ['github-repositories'] }) } })
  const retry = useMutation({
    mutationFn: github.retry,
    onSuccess: (_, id) => client.invalidateQueries({ queryKey: ['analysis-job', id] }),
  })
  const visible = useMemo(() => repos.data?.filter(item => item.fullName.toLowerCase().includes(search.toLowerCase())) ?? [], [repos.data, search])
  function toggleRepository(gitHubId: number, checked: boolean) {
    setSelectionTouched(true)
    setSelected(checked ? [...selected, gitHubId] : selected.filter(id => id !== gitHubId))
  }
  if (status.isPending) return <Loading message="Checking GitHub connection…" />
  if (status.isError) return <ErrorNotice message={status.error.message} retry={() => void status.refetch()} />
  if (!status.data.connected) return <section className="card github-connect"><h1>GitHub evidence</h1><p className="muted">ProofPath requests read-only Metadata and Contents access. Public repositories are never analyzed until you select them here.</p>{connect.isError && <ErrorNotice message={connect.error.message} />}<button onClick={() => connect.mutate()} disabled={connect.isPending}>{connect.isPending ? 'Preparing GitHub…' : 'Connect GitHub'}</button></section>
  return <section className="github-workspace" aria-labelledby="github-title">
    <div className="page-heading"><h1 id="github-title">GitHub evidence</h1><p className="muted">Connected as {status.data.login} · installation on {status.data.targetLogin}. Analysis is deterministic and fixed to a commit SHA.</p></div>
    <div className="card repository-picker"><div className="row-between"><div><h3>Select repositories</h3><p className="hint">Choose up to 5. GitHub access alone never counts as consent.</p></div><button className="secondary" onClick={() => synchronize.mutate()} disabled={synchronize.isPending}>{synchronize.isPending ? 'Refreshing…' : 'Refresh'}</button></div>
      {repos.isPending ? <Loading message="Loading authorized repositories…" /> : repos.isError ? <ErrorNotice message={repos.error.message} retry={() => void repos.refetch()} /> : <><label>Search repositories<input name="repositorySearch" type="search" autoComplete="off" spellCheck={false} value={search} onChange={event => setSearch(event.target.value)} placeholder="owner/repository…" /></label><p className="selection-count">{selected.length} of 5 selected</p><div className="repository-list">{visible.map(repository => <label className="repository-option" key={repository.id}><input type="checkbox" name="selectedRepositories" autoComplete="off" value={String(repository.gitHubId)} checked={selected.includes(repository.gitHubId)} disabled={!selected.includes(repository.gitHubId) && selected.length >= 5} onChange={event => toggleRepository(repository.gitHubId, event.target.checked)} /><span><strong>{repository.fullName}</strong><small>{repository.private ? 'Private' : 'Public'} · {repository.defaultBranch} · {repository.scanStatus}</small></span></label>)}</div>{save.isError && <ErrorNotice message={save.error.message} />}<div className="button-row"><button className="secondary" onClick={() => save.mutate()} disabled={save.isPending}>{save.isPending ? 'Saving…' : 'Save selection'}</button><button onClick={() => scan.mutate()} disabled={selected.length === 0 || scan.isPending || save.isPending}>{scan.isPending ? 'Queueing…' : 'Analyze selected'}</button></div></>}
      {scan.isError && <ErrorNotice message={scan.error.message} />}
      {synchronize.isError && <ErrorNotice message={synchronize.error.message} />}
      {retry.isError && <ErrorNotice message={retry.error.message} />}
      {progress.length > 0 && <div className="scan-progress" aria-live="polite">
        <h4>Analysis progress</h4>
        {progress.map((item, index) => {
          const job = item.data
          const code = job?.errorCode
          const state = job?.state === 'Pending' && code ? 'Waiting to retry' : job?.state ?? 'Pending…'
          return <p key={jobs[index]}>
            {item.isError ? 'Status unavailable' : state}
            {code && ` · ${scanMessages[code] ?? 'The analysis could not finish.'} (${code})`}
            {job?.state === 'Failed' && job.retryable &&
              <button className="link-button" disabled={retry.isPending} onClick={() => retry.mutate(jobs[index])}>Retry</button>}
          </p>
        })}
      </div>}
    </div>
    <div className="card"><h3>GitHub evidence</h3>{evidence.isPending ? <Loading /> : evidence.isError ? <ErrorNotice message={evidence.error.message} retry={() => void evidence.refetch()} /> : evidence.data.length === 0 ? <p className="muted">No repository evidence yet. Save a selection and run analysis.</p> : <div className="evidence-list">{evidence.data.map(item => <article key={item.id} className="evidence-card"><div className="row-between"><strong>{item.originalTerm}</strong><span className={`badge ${item.lifecycle.toLowerCase()}`}>{item.strength} · {item.lifecycle}</span></div><p>{item.detail}</p><small>{item.repository} · {item.revisionSha.slice(0, 8)}{item.sourcePath ? ` · ${item.sourcePath}` : ''} · {Math.round(item.extractionConfidence * 100)}% extraction confidence</small></article>)}</div>}</div>
    <div className="card disconnect"><h3>Disconnect GitHub</h3><p className="muted">New access and scans stop. Historical evidence remains marked stale until you delete your ProofPath account.</p><a className="button secondary" href="/settings">Administrar acceso y privacidad ↗</a></div>
  </section>
}
