import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { github } from '../api/github'
import { ErrorNotice, Loading } from '../components/Feedback'
import './GitHubPage.css'

const terminal = ['Completed', 'PartiallyCompleted', 'Failed', 'Cancelled']
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
  const scan = useMutation({ mutationFn: github.scan, onSuccess: result => setJobs(result.analysisJobIds) })
  const disconnect = useMutation({ mutationFn: github.disconnect, onSuccess: () => { setJobs([]); client.removeQueries({ queryKey: ['github-repositories'] }); client.removeQueries({ queryKey: ['github-evidence'] }); void client.invalidateQueries({ queryKey: ['github-status'] }) } })
  const visible = useMemo(() => repos.data?.filter(item => item.fullName.toLowerCase().includes(search.toLowerCase())) ?? [], [repos.data, search])
  function toggleRepository(gitHubId: number, checked: boolean) {
    setSelectionTouched(true)
    setSelected(checked ? [...selected, gitHubId] : selected.filter(id => id !== gitHubId))
  }
  if (status.isPending) return <Loading message="Checking GitHub connection…" />
  if (status.isError) return <ErrorNotice message={status.error.message} retry={() => void status.refetch()} />
  if (!status.data.connected) return <section className="card github-connect"><p className="eyebrow">GITHUB EVIDENCE</p><h2>Connect repositories you choose.</h2><p className="muted">ProofPath requests read-only Metadata and Contents access. Public repositories are never analyzed until you select them here.</p>{connect.isError && <ErrorNotice message={connect.error.message} />}<button onClick={() => connect.mutate()} disabled={connect.isPending}>{connect.isPending ? 'Preparing GitHub…' : 'Connect GitHub'}</button></section>
  return <section className="github-workspace" aria-labelledby="github-title">
    <div className="page-heading"><p className="eyebrow">GITHUB EVIDENCE</p><h2 id="github-title">Evidence from selected code snapshots.</h2><p className="muted">Connected as {status.data.login} · installation on {status.data.targetLogin}. Analysis is deterministic and fixed to a commit SHA.</p></div>
    <div className="card repository-picker"><div className="row-between"><div><h3>Select repositories</h3><p className="hint">Choose up to 5. GitHub access alone never counts as consent.</p></div><button className="secondary" onClick={() => synchronize.mutate()} disabled={synchronize.isPending}>{synchronize.isPending ? 'Refreshing…' : 'Refresh'}</button></div>
      {repos.isPending ? <Loading message="Loading authorized repositories…" /> : repos.isError ? <ErrorNotice message={repos.error.message} retry={() => void repos.refetch()} /> : <><label>Search repositories<input value={search} onChange={event => setSearch(event.target.value)} placeholder="owner/repository" /></label><p className="selection-count">{selected.length} of 5 selected</p><div className="repository-list">{visible.map(repository => <label className="repository-option" key={repository.id}><input type="checkbox" checked={selected.includes(repository.gitHubId)} disabled={!selected.includes(repository.gitHubId) && selected.length >= 5} onChange={event => toggleRepository(repository.gitHubId, event.target.checked)} /><span><strong>{repository.fullName}</strong><small>{repository.private ? 'Private' : 'Public'} · {repository.defaultBranch} · {repository.scanStatus}</small></span></label>)}</div>{save.isError && <ErrorNotice message={save.error.message} />}<div className="button-row"><button className="secondary" onClick={() => save.mutate()} disabled={save.isPending}>{save.isPending ? 'Saving…' : 'Save selection'}</button><button onClick={() => scan.mutate()} disabled={selected.length === 0 || scan.isPending || save.isPending}>{scan.isPending ? 'Queueing…' : 'Analyze selected'}</button></div></>}
      {scan.isError && <ErrorNotice message={scan.error.message} />}{progress.length > 0 && <div className="scan-progress" aria-live="polite"><h4>Analysis progress</h4>{progress.map((item, index) => <p key={jobs[index]}>{item.isError ? 'Status unavailable' : item.data ? `${item.data.state}${item.data.errorCode ? ` · ${item.data.errorCode}` : ''}` : 'Pending…'} {item.data?.state === 'Failed' && item.data.retryable && <button className="link-button" onClick={() => void github.retry(jobs[index]).then(() => item.refetch())}>Retry</button>}</p>)}</div>}
    </div>
    <div className="card"><h3>GitHub evidence</h3>{evidence.isPending ? <Loading /> : evidence.isError ? <ErrorNotice message={evidence.error.message} retry={() => void evidence.refetch()} /> : evidence.data.length === 0 ? <p className="muted">No repository evidence yet. Save a selection and run analysis.</p> : <div className="evidence-list">{evidence.data.map(item => <article key={item.id} className="evidence-card"><div className="row-between"><strong>{item.originalTerm}</strong><span className={`badge ${item.lifecycle.toLowerCase()}`}>{item.strength} · {item.lifecycle}</span></div><p>{item.detail}</p><small>{item.repository} · {item.revisionSha.slice(0, 8)}{item.sourcePath ? ` · ${item.sourcePath}` : ''} · {Math.round(item.extractionConfidence * 100)}% extraction confidence</small></article>)}</div>}</div>
    <div className="card disconnect"><h3>Disconnect GitHub</h3><p className="muted">New access and scans stop. Historical evidence remains marked stale until you delete your ProofPath account.</p>{disconnect.isError && <ErrorNotice message={disconnect.error.message} />}<button className="danger" onClick={() => disconnect.mutate()} disabled={disconnect.isPending}>{disconnect.isPending ? 'Disconnecting…' : 'Disconnect GitHub'}</button></div>
  </section>
}
