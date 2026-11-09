import { useCallback, useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ErrorNotice, Loading } from '../components/Feedback'
import { jobs } from '../api/jobs'
import type { ApplicationStage, JobInput, JobReview, JobSummary, JobTracking, MatchView, RecommendationStatus, RecommendationView, RequirementDraft, RequirementDraftSet, ScoreHistoryPoint, SkillOption } from '../api/jobs'
import './JobsPage.css'
import { api } from '../api/client'

const terminal = ['Completed', 'PartiallyCompleted', 'Failed', 'Cancelled']
const categories = ['TechnicalSkill', 'Experience', 'EducationCredential', 'Behavioral', 'Contextual'] as const
const levels = ['Required', 'Preferred', 'Unspecified'] as const
const importance = ['Critical', 'High', 'Medium', 'Low'] as const
const groupTypes = ['None', 'AnyOf', 'AllOf'] as const
const applicationStages: ApplicationStage[] = ['Saved', 'Preparing', 'Applied', 'Interviewing', 'Offer', 'Rejected', 'Withdrawn']
const recommendationStatuses: RecommendationStatus[] = ['Open', 'InProgress', 'Completed', 'Dismissed']
const labels: Record<string, string> = {
  TechnicalSkill: 'Technical skills', Experience: 'Experience', EducationCredential: 'Education & credentials',
  Behavioral: 'Behavioral evidence', Contextual: 'Context only',
}

const blank: JobInput = { company: '', title: '', description: '', sourceUrl: '' }
function cleaned(value: JobInput): JobInput {
  return { ...value, company: value.company?.trim() || null, title: value.title?.trim() || null, sourceUrl: value.sourceUrl?.trim() || null }
}

export function JobsPage() {
  const queryClient = useQueryClient()
  const listing = useQuery({ queryKey: ['jobs'], queryFn: jobs.list, refetchInterval: query => query.state.data?.some(job => job.status === 'Processing') ? 1800 : false })
  const [selected, setSelected] = useState<string | null>(() => new URLSearchParams(window.location.search).get('job'))
  const [input, setInput] = useState<JobInput>(blank)
  const create = useMutation({
    mutationFn: () => jobs.create(cleaned(input)),
    onSuccess: result => { setInput(blank); setSelected(result.job.id); void queryClient.invalidateQueries({ queryKey: ['jobs'] }) },
  })
  const active = listing.data?.find(job => job.id === selected) ?? null
  const refreshJobs = useCallback(() => { void queryClient.invalidateQueries({ queryKey: ['jobs'] }) }, [queryClient])
  if (listing.isPending) return <Loading message="Loading saved jobs…" />
  if (listing.isError) return <ErrorNotice message={listing.error.message} retry={() => void listing.refetch()} />
  return <section className="jobs-workspace" aria-labelledby="jobs-title">
    <div className="page-heading"><p className="context-line">OPPORTUNITY MATCHER</p><h1 id="jobs-title">Encuentra el encaje.<br />Entiende el porqué.</h1><p className="muted">Tu experiencia frente a lo que pide el puesto. Revisa los requisitos extraídos antes de comparar tus evidencias.</p></div>
    <details className="job-disclosure" open={listing.data.length === 0 ? true : undefined}><summary>Add an opportunity</summary>
    <div className="matcher-entry"><JobForm title="La oportunidad." input={input} setInput={setInput} submit={() => create.mutate()} pending={create.isPending} error={create.error?.message} /><CandidateSnapshot title={input.title} /></div>
    </details>
    <div className="jobs-layout">
      <section className="card job-list" aria-label="Saved jobs"><div className="row-between"><h2>Saved jobs</h2><span className="hint">{listing.data.length} total</span></div>
        {listing.data.length === 0 ? <p className="muted">No jobs saved yet.</p> : listing.data.map(job => <button key={job.id} className={'job-list-item ' + (job.id === selected ? 'selected' : '')} onClick={() => setSelected(job.id)}>
          <span><strong>{job.title || 'Untitled role'}</strong><small>{job.company || 'Company not specified'}</small></span><span className={'job-status ' + job.status.toLowerCase()}>{job.status === 'ReadyForReview' ? 'Ready for review' : job.status}</span>
        </button>)}
      </section>
      <div>{active ? <JobDetail key={active.id + '-' + active.descriptionVersion + '-' + active.updatedAt} job={active} refresh={refreshJobs} /> : <section className="card empty-job"><h2>Select a saved job</h2><p className="muted">You can update its source text, monitor extraction, review grouped requirements, and confirm an immutable requirement set.</p></section>}</div>
    </div>
  </section>
}

function CandidateSnapshot({ title }: { title: string | null }) {
  const profile = useQuery({ queryKey: ['profile'], queryFn: api.profile })
  return <div><div className="snapshot-pair"><article className="snapshot-card" data-layer="user-snapshot"><span className="hint">User snapshot</span><h3>{profile.isPending ? 'Tu perfil…' : profile.data ? [profile.data.firstName, profile.data.lastName].filter(Boolean).join(' ') || 'Tu perfil' : 'Tu perfil'}</h3><p>{profile.data?.headline || 'Las evidencias activas de tu currículum y tus proyectos.'}</p><a href="/home">Revisar dossier ↗</a></article><span className="snapshot-exchange" data-layer="intercambio" aria-hidden="true">⇄</span><article className="snapshot-card job" data-layer="job-requirements"><span className="hint">Job requirements</span><h3>{title || 'El siguiente paso'}</h3><p>Texto original → requisitos revisados → comparación explicable.</p></article></div>{profile.isError && <ErrorNotice message={profile.error.message} retry={() => void profile.refetch()} />}<p className="hint">La confianza describe el respaldo de la evidencia. No es una probabilidad de contratación.</p></div>
}

function JobForm({ title, input, setInput, submit, pending, error, submitLabel = 'Save and analyze' }: {
  title: string; input: JobInput; setInput: (value: JobInput) => void; submit: () => void; pending: boolean; error?: string; submitLabel?: string
}) {
  const valid = input.description.trim().length >= 100 && input.description.trim().length <= 50000
  return <section className="card job-form"><h2>{title}</h2><div className="form-grid"><label>Company <span className="hint">Recommended</span><input name="company" autoComplete="off" value={input.company ?? ''} maxLength={255} onChange={e => setInput({ ...input, company: e.target.value })} /></label><label>Role title <span className="hint">Recommended</span><input name="title" autoComplete="off" value={input.title ?? ''} maxLength={255} onChange={e => setInput({ ...input, title: e.target.value })} /></label></div>
    <label>Source URL <span className="hint">Optional metadata; ProofPath does not scrape this page.</span><input name="sourceUrl" type="url" autoComplete="off" spellCheck={false} value={input.sourceUrl ?? ''} maxLength={2048} placeholder="https://…" onChange={e => setInput({ ...input, sourceUrl: e.target.value })} /></label>
    <label>Job description <span className="hint">Required · 100–50,000 characters</span><textarea name="description" autoComplete="off" rows={10} value={input.description} onChange={e => setInput({ ...input, description: e.target.value })} /></label>
    <div className="row-between"><span className={'character-count ' + (valid ? '' : 'invalid')}>{input.description.trim().length.toLocaleString()} / 50,000</span><button disabled={!valid || pending} onClick={submit}>{pending ? 'Saving…' : submitLabel}</button></div>
    {error && <ErrorNotice message={error} />}
  </section>
}

function JobDetail({ job, refresh }: { job: JobSummary; refresh: () => void }) {
  const [input, setInput] = useState<JobInput>({ company: job.company, title: job.title, description: job.description, sourceUrl: job.sourceUrl, descriptionVersion: job.descriptionVersion })
  const status = useQuery({ queryKey: ['job-analysis', job.analysisJobId], queryFn: () => jobs.status(job.analysisJobId!), enabled: Boolean(job.analysisJobId) && job.status === 'Processing', refetchInterval: query => terminal.includes(query.state.data?.state ?? '') ? false : 1500 })
  const update = useMutation({ mutationFn: () => jobs.update(job.id, cleaned(input)), onSuccess: refresh })
  const retry = useMutation({ mutationFn: () => jobs.retry(job.analysisJobId!), onSuccess: () => void status.refetch() })
  useEffect(() => { if (status.data && terminal.includes(status.data.state)) refresh() }, [status.data, refresh])
  return <div className="job-detail"><div className="row-between"><div><h2>{job.title || 'Untitled role'}</h2><p className="muted">{job.company || 'Company not specified'}</p></div><span className="badge">{job.status === 'ReadyForReview' ? 'Ready for review' : job.status}</span></div><details className="job-disclosure"><summary>View or edit job description</summary><JobForm title={'Edit ' + (job.title || 'job')} input={input} setInput={setInput} submit={() => update.mutate()} pending={update.isPending} error={update.error?.message} submitLabel="Save changes" /></details>
    {job.status === 'Processing' && <section className="notice" role="status"><strong>Requirement extraction is running.</strong><p>{status.data ? 'Worker state: ' + status.data.state + (status.data.errorCode ? ' · ' + status.data.errorCode : '') : 'Waiting for the worker…'}</p>{status.isError && <ErrorNotice message={status.error.message} retry={() => void status.refetch()} />}{status.data?.state === 'Failed' && status.data.retryable && <button onClick={() => retry.mutate()} disabled={retry.isPending}>Retry extraction</button>}</section>}
    {job.status === 'ReadyForReview' && <RequirementReview job={job} refresh={refresh} />}
    {job.status === 'Confirmed' && <><MatchPanel jobId={job.id} /><TrackingPanel jobId={job.id} /><details className="job-disclosure"><summary>Inspect confirmed requirements</summary><RequirementReview job={job} refresh={refresh} /></details></>}
  </div>
}

function TrackingPanel({ jobId }: { jobId: string }) {
  const tracking = useQuery({ queryKey: ['job-tracking', jobId], queryFn: () => jobs.tracking(jobId) })
  if (tracking.isPending) return <Loading message="Loading application tracking…" />
  if (tracking.isError) return <ErrorNotice message={tracking.error.message} retry={() => void tracking.refetch()} />
  return <TrackingForm key={tracking.data.updatedAt} jobId={jobId} initial={tracking.data} />
}

function TrackingForm({ jobId, initial }: { jobId: string; initial: JobTracking }) {
  const [stage, setStage] = useState<ApplicationStage>(initial.stage)
  const [notes, setNotes] = useState(initial.notes ?? '')
  const [nextActionAt, setNextActionAt] = useState(initial.nextActionAt?.slice(0, 10) ?? '')
  const save = useMutation({
    mutationFn: () => jobs.updateTracking(jobId, { stage, notes: notes.trim() || null, nextActionAt: nextActionAt ? new Date(nextActionAt + 'T12:00:00').toISOString() : null }),
  })
  return <section className="card tracking-panel" aria-labelledby="tracking-title">
    <div className="row-between"><div><h2 id="tracking-title">Keep the next step visible</h2></div><span className={'stage-badge ' + stage.toLowerCase()}>{stage}</span></div>
    <div className="tracking-grid"><label>Status<select name="applicationStage" autoComplete="off" value={stage} onChange={event => setStage(event.target.value as ApplicationStage)}>{applicationStages.map(value => <option key={value}>{value}</option>)}</select></label><label>Next action date<input name="nextActionAt" aria-label="Next action date" type="date" autoComplete="off" value={nextActionAt} onChange={event => setNextActionAt(event.target.value)} /></label></div>
    <label>Notes<textarea name="applicationNotes" aria-label="Application notes" autoComplete="off" rows={3} maxLength={2000} value={notes} onChange={event => setNotes(event.target.value)} placeholder="Contact, interview preparation, or follow-up details…" /></label>
    <div className="row-between"><span className="hint">{notes.length.toLocaleString()} / 2,000</span><button onClick={() => save.mutate()} disabled={save.isPending}>{save.isPending ? 'Saving…' : 'Save tracking'}</button></div>
    {save.isSuccess && <p className="notice success" role="status">Application tracking saved.</p>}
    {save.isError && <ErrorNotice message={save.error.message} />}
  </section>
}

function RequirementReview({ job, refresh }: { job: JobSummary; refresh: () => void }) {
  const review = useQuery({ queryKey: ['job-review', job.id, job.descriptionVersion], queryFn: () => jobs.review(job.id) })
  if (review.isPending) return <Loading message="Loading extracted requirements…" />
  if (review.isError) return <ErrorNotice message={review.error.message} retry={() => void review.refetch()} />
  return <ReviewEditor key={review.data.revision + '-' + review.data.confirmedAt} review={review.data} onChanged={() => { void review.refetch(); refresh() }} />
}

export function ReviewEditor({ review, onChanged }: { review: JobReview; onChanged?: () => void }) {
  const [draft, setDraft] = useState<RequirementDraftSet>(review.draft)
  const [acknowledged, setAcknowledged] = useState(false)
  const [dirty, setDirty] = useState(false)
  const skills = useQuery({ queryKey: ['job-skills'], queryFn: jobs.skills })
  const locked = Boolean(review.confirmedAt)
  const save = useMutation({ mutationFn: () => jobs.saveReview(review.jobId, review.revision, draft), onSuccess: () => { setDirty(false); setAcknowledged(false); onChanged?.() } })
  const confirm = useMutation({ mutationFn: () => jobs.confirm(review.jobId, review.revision), onSuccess: () => onChanged?.() })
  const grouped = useMemo(() => categories.map(category => ({ category, items: draft.requirements.map((item, index) => ({ item, index })).filter(entry => entry.item.category === category) })).filter(group => group.items.length), [draft])
  function change(index: number, value: RequirementDraft) {
    setDraft({ requirements: draft.requirements.map((item, itemIndex) => itemIndex === index ? value : item) }); setAcknowledged(false); setDirty(true)
  }
  return <section className="card requirement-review"><div className="row-between"><div><h2>Version {review.descriptionVersion}</h2></div><span className="badge">{review.confirmedAt ? 'Confirmed set' : 'Review required'}</span></div>
    {review.machine.source.warnings.map(warning => <p className="notice" key={warning}>{warning}</p>)}
    {locked && <p className="notice success" role="status">This requirement set is confirmed and immutable. Edit the job description to create a new version.</p>}
    {!locked && <p className="hint">Required/preferred and importance are separate. “Any of” records alternatives; “all of” records combined requirements. Excluded and unresolved items remain auditable and do not score.</p>}
    {grouped.map(group => <section className="requirement-group" key={group.category}><h3>{labels[group.category]}</h3>{group.category === 'Behavioral' && <p className="hint">Visible for review, but behavioral scoring is disabled.</p>}{group.category === 'Contextual' && <p className="hint">Context is retained without treating it as a candidate requirement.</p>}
      {group.items.map(({ item, index }) => <RequirementRow key={item.key} item={item} index={index} skills={skills.data ?? []} locked={locked} change={change} />)}
    </section>)}
    {!locked && <>{dirty && <p className="notice">Guarda los cambios antes de confirmar los requisitos.</p>}<label className="review-ack"><input type="checkbox" name="requirementReviewAcknowledged" autoComplete="off" disabled={dirty || save.isPending || confirm.isPending} checked={acknowledged} onChange={e => setAcknowledged(e.target.checked)} />I reviewed the source wording, alternatives, exclusions, and unresolved skills.</label><div className="actions"><button className="secondary" onClick={() => save.mutate()} disabled={save.isPending || confirm.isPending}>{save.isPending ? 'Saving…' : 'Save review'}</button><button onClick={() => confirm.mutate()} disabled={dirty || !acknowledged || confirm.isPending || save.isPending}>{confirm.isPending ? 'Confirming…' : 'Confirm requirement set'}</button></div></>}
    {(save.isError || confirm.isError) && <ErrorNotice message={(save.error ?? confirm.error)!.message} />}
  </section>
}

function RequirementRow({ item, index, skills, locked, change }: { item: RequirementDraft; index: number; skills: SkillOption[]; locked: boolean; change: (index: number, item: RequirementDraft) => void }) {
  const update = (values: Partial<RequirementDraft>) => change(index, { ...item, ...values })
  const unresolved = item.category === 'TechnicalSkill' && !item.skillId
  return <article className={'requirement-row ' + (item.state === 'Excluded' ? 'excluded' : '')}><div className="row-between"><strong>{item.originalWording}</strong><span className={'normalization ' + (unresolved ? 'unresolved' : '')}>{unresolved ? 'Unresolved · not evaluated' : item.normalizationStatus}</span></div>
    <blockquote>“{item.quote}” <cite>{item.sourceBlockId}</cite></blockquote>
    <div className="requirement-controls"><label>Level<select name={`requirementLevel-${index}`} autoComplete="off" disabled={locked} value={item.level} onChange={e => update({ level: e.target.value as RequirementDraft['level'] })}>{levels.map(value => <option key={value}>{value}</option>)}</select></label><label>Importance<select name={`requirementImportance-${index}`} autoComplete="off" disabled={locked} value={item.importance} onChange={e => update({ importance: e.target.value as RequirementDraft['importance'] })}>{importance.map(value => <option key={value}>{value}</option>)}</select></label><label>State<select name={`requirementState-${index}`} autoComplete="off" disabled={locked} value={item.state} onChange={e => update({ state: e.target.value as RequirementDraft['state'] })}><option>Extracted</option><option>Excluded</option></select></label></div>
    {item.category === 'TechnicalSkill' && <label>Canonical skill<select name={`requirementSkill-${index}`} autoComplete="off" disabled={locked} value={item.skillId ?? ''} onChange={e => update({ skillId: e.target.value || null, normalizationStatus: e.target.value ? 'UserConfirmed' : 'Unresolved' })}><option value="">Unresolved / not evaluated</option>{skills.map(skill => <option key={skill.id} value={skill.id}>{skill.displayName}</option>)}</select></label>}
    <div className="requirement-controls group-controls"><label>Relationship<select name={`requirementRelationship-${index}`} autoComplete="off" disabled={locked} value={item.groupType} onChange={e => update({ groupType: e.target.value as RequirementDraft['groupType'], groupKey: e.target.value === 'None' ? null : item.groupKey || 'group-' + (index + 1) })}>{groupTypes.map(value => <option key={value}>{value}</option>)}</select></label>{item.groupType !== 'None' && <label>Group key<input name={`requirementGroup-${index}`} autoComplete="off" disabled={locked} value={item.groupKey ?? ''} onChange={e => update({ groupKey: e.target.value })} /></label>}</div>
    {item.qualifiers.length > 0 && <p className="qualifiers">Qualifiers: {item.qualifiers.join(' · ')}</p>}
  </article>
}
function MatchPanel({ jobId }: { jobId: string }) {
  const queryClient = useQueryClient()
  const latest = useQuery({ queryKey: ['job-match', jobId], queryFn: () => jobs.latestMatch(jobId) })
  const history = useQuery({ queryKey: ['job-match-history', jobId], queryFn: () => jobs.matches(jobId) })
  const recommendations = useQuery({ queryKey: ['job-recommendations', jobId], queryFn: () => jobs.recommendations(jobId) })
  const scoreHistory = useQuery({ queryKey: ['job-score-history', jobId], queryFn: () => jobs.scoreHistory(jobId) })
  const calculate = useMutation({
    mutationFn: () => latest.data ? jobs.rescan(jobId) : jobs.calculateMatch(jobId),
    onSuccess: result => {
      queryClient.setQueryData(['job-match', jobId], result)
      void queryClient.invalidateQueries({ queryKey: ['job-match-history', jobId] })
      void queryClient.invalidateQueries({ queryKey: ['job-recommendations', jobId] })
      void queryClient.invalidateQueries({ queryKey: ['job-score-history', jobId] })
    },
  })
  if (latest.isPending) return <Loading message="Loading latest match…" />
  if (latest.isError) return <ErrorNotice message={latest.error.message} retry={() => void latest.refetch()} />
  return <section className="card match-panel" aria-labelledby="match-title">
    <div className="row-between"><div><h2 id="match-title">Candidate fit</h2></div><button onClick={() => calculate.mutate()} disabled={calculate.isPending}>{calculate.isPending ? 'Analyzing…' : latest.data ? 'Rescan evidence' : 'Calculate match'}</button></div>
    <p className="hint">Uses confirmed requirements and candidate evidence with matching-v1. AI does not set scores, gaps, or priorities.</p>
    {history.isError && <ErrorNotice message={history.error.message} retry={() => void history.refetch()} />}
    {calculate.isError && <ErrorNotice message={calculate.error.message} />}
    {latest.data ? <><MatchResultView match={latest.data} historyCount={history.data?.length ?? 1} /><>{recommendations.isError ? <ErrorNotice message={recommendations.error.message} retry={() => void recommendations.refetch()} /> : <RecommendationPanel jobId={jobId} items={recommendations.data ?? []} loading={recommendations.isPending} />}</><>{scoreHistory.isError ? <ErrorNotice message={scoreHistory.error.message} retry={() => void scoreHistory.refetch()} /> : <ScoreHistory items={scoreHistory.data ?? []} loading={scoreHistory.isPending} />}</></> : <div className="empty-match"><strong>No match calculated yet.</strong><p className="muted">A result is stored as an immutable snapshot so future evidence or requirement changes do not rewrite history.</p></div>}
  </section>
}

function RecommendationPanel({ jobId, items, loading }: { jobId: string; items: RecommendationView[]; loading: boolean }) {
  const queryClient = useQueryClient()
  const update = useMutation({
    mutationFn: ({ id, status }: { id: string; status: RecommendationStatus }) => jobs.updateRecommendation(jobId, id, status),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['job-recommendations', jobId] }),
  })
  return <section className="recommendations" aria-labelledby="recommendations-title"><div className="row-between"><h3 id="recommendations-title">Next best actions</h3><span className="hint">Ranked from the latest match</span></div>
    {loading ? <p className="muted">Loading recommendations…</p> : items.length === 0 ? <p className="muted">Calculate a match to generate recommendations.</p> : <ol>{items.map(item => <li key={item.id} className={'recommendation ' + item.status.toLowerCase()}><span className="recommendation-rank">{item.rank}</span><div><div className="row-between"><strong>{item.title}</strong><select name={`recommendationStatus-${item.id}`} autoComplete="off" aria-label={'Status for ' + item.title} value={item.status} onChange={event => update.mutate({ id: item.id, status: event.target.value as RecommendationStatus })}>{recommendationStatuses.map(value => <option key={value}>{value}</option>)}</select></div><p>{item.rationale}</p><span className="recommendation-action">{item.action}</span></div></li>)}</ol>}
    {update.isError && <ErrorNotice message={update.error.message} />}
  </section>
}

function ScoreHistory({ items, loading }: { items: ScoreHistoryPoint[]; loading: boolean }) {
  return <section className="score-history" aria-labelledby="score-history-title"><div className="row-between"><h3 id="score-history-title">Score history</h3><span className="hint">Immutable comparisons</span></div>
    {loading ? <p className="muted">Loading score history…</p> : items.length === 0 ? <p className="muted">No saved results yet.</p> : <div className="score-timeline">{items.map((item, index) => <article key={item.matchResultId}><span>{index === 0 ? 'Latest' : new Date(item.createdAt).toLocaleDateString()}</span><strong>{item.score === null ? 'Limited' : Math.round(item.score) + '%'}</strong><small className={item.delta === null ? '' : item.delta >= 0 ? 'positive' : 'negative'}>{item.delta === null ? 'Baseline' : (item.delta >= 0 ? '+' : '') + Math.round(item.delta) + ' points'} · coverage {Math.round(item.coverage * 100)}%</small></article>)}</div>}
  </section>
}

function MatchResultView({ match, historyCount }: { match: MatchView; historyCount: number }) {
  const result = match.result
  const percent = (value: number) => Math.round(value * 100) + '%'
  return <div className="match-result">
    <div className={'match-hero ' + result.status.toLowerCase()}>
      <div><span className="hint">Overall result</span><strong>{result.overallScore === null ? 'Limited' : Math.round(result.overallScore) + '%'}</strong><span>{result.classification?.replace(/([A-Z])/g, ' $1').trim() ?? 'Insufficient coverage'}</span></div>
      <dl><div><dt>Status</dt><dd>{result.status}</dd></div><div><dt>Confidence</dt><dd>{result.confidenceBand} · {percent(result.overallConfidence)}</dd></div><div><dt>Coverage</dt><dd>{percent(result.evaluationCoverage)}</dd></div><div><dt>Required coverage</dt><dd>{result.requiredCoverage === null ? 'N/A' : percent(result.requiredCoverage)}</dd></div><div><dt>Version</dt><dd>{result.scoringVersion}</dd></div></dl>
    </div>
    {result.status !== 'Complete' && <p className="notice">Coverage is {result.status === 'Limited' ? 'too limited for a reliable overall classification' : 'partial; review uncertain requirements before acting on this result'}.</p>}
    {result.safeguards.length > 0 && <p className="notice">Safeguards: {result.safeguards.join(' · ')}</p>}
    <div className="match-components">{result.components.map(component => <article key={component.name}><span>{component.name}</span><strong>{component.score === null ? component.status.replace(/([a-z])([A-Z])/g, '$1 $2') : Math.round(component.score) + '%'}</strong><small>Coverage {percent(component.coverage)}</small></article>)}</div>
    <section><h3>Requirement trace</h3><div className="match-key" aria-label="Estados de evaluación"><span className="match-state">Respaldado</span><span className="match-state missing">Missing</span><span className="match-state uncertain">Uncertain</span><span className="match-state notevaluated">NotEvaluated</span></div>{result.requirements.map(requirement => <details className={'match-requirement ' + requirement.evaluationStatus.toLowerCase()} key={requirement.requirementId}><summary><span><strong>{requirement.originalWording}</strong><small>{requirement.level} · {requirement.importance}</small></span><span className={'match-state ' + (requirement.evaluationStatus !== 'Evaluated' ? requirement.evaluationStatus.toLowerCase() : requirement.classification === 'Missing' ? 'missing' : 'supported')}>{requirement.evaluationStatus !== 'Evaluated' ? requirement.evaluationStatus : requirement.classification}{requirement.score === null ? '' : ' · ' + Math.round(requirement.score) + '%'}</span></summary><p>{requirement.details}</p><p className="hint">{requirement.reasonCode} · Confidence {percent(requirement.confidence)}</p>{requirement.evidence.map(item => <blockquote key={item.evidenceId}>“{item.quote || 'Evidence recorded without a quote.'}” <cite>{item.sourceReference} · {item.strength} · contribution {percent(item.contribution)}</cite></blockquote>)}</details>)}</section>
    <section><div className="row-between"><h3>Gaps and verification</h3><span className="hint">{result.gaps.length} identified</span></div>{result.gaps.length === 0 ? <p className="muted">No scored gaps in this snapshot.</p> : <ul className="match-gaps">{result.gaps.map(gap => <li key={gap.requirementId}><strong>{gap.priority}: {gap.requirement}</strong><span>{gap.type} · {gap.reason}</span></li>)}</ul>}</section>
    {result.behavioralAssessment.length > 0 && <section><h3>Behavioral evidence</h3><p className="hint">Reported separately. It never changes the percentage.</p>{result.behavioralAssessment.map(item => <p key={item.themeKey}><strong>{item.themeKey}:</strong> {item.summary}</p>)}</section>}
    <p className="match-history">Result {match.id.slice(0, 8)} · {new Date(match.createdAt).toLocaleString()} · {historyCount} immutable result{historyCount === 1 ? '' : 's'} stored</p>
  </div>
}
