import { useCallback, useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ErrorNotice, Loading } from '../components/Feedback'
import { jobs } from '../api/jobs'
import type { JobInput, JobReview, JobSummary, MatchView, RequirementDraft, RequirementDraftSet, SkillOption } from '../api/jobs'
import './JobsPage.css'

const terminal = ['Completed', 'PartiallyCompleted', 'Failed', 'Cancelled']
const categories = ['TechnicalSkill', 'Experience', 'EducationCredential', 'Behavioral', 'Contextual'] as const
const levels = ['Required', 'Preferred', 'Unspecified'] as const
const importance = ['Critical', 'High', 'Medium', 'Low'] as const
const groupTypes = ['None', 'AnyOf', 'AllOf'] as const
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
  const [selected, setSelected] = useState<string | null>(null)
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
    <div className="page-heading"><p className="eyebrow">JOB INTELLIGENCE</p><h1 id="jobs-title">Turn a job description into reviewable requirements.</h1><p className="muted">ProofPath stores the original description first, then extracts only requirements supported by its wording. Candidate evidence is never sent into this analysis.</p></div>
    <JobForm title="Add a job" input={input} setInput={setInput} submit={() => create.mutate()} pending={create.isPending} error={create.error?.message} />
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

function JobForm({ title, input, setInput, submit, pending, error, submitLabel = 'Save and analyze' }: {
  title: string; input: JobInput; setInput: (value: JobInput) => void; submit: () => void; pending: boolean; error?: string; submitLabel?: string
}) {
  const valid = input.description.trim().length >= 100 && input.description.trim().length <= 50000
  return <section className="card job-form"><h2>{title}</h2><div className="form-grid"><label>Company <span className="hint">Recommended</span><input value={input.company ?? ''} maxLength={255} onChange={e => setInput({ ...input, company: e.target.value })} /></label><label>Role title <span className="hint">Recommended</span><input value={input.title ?? ''} maxLength={255} onChange={e => setInput({ ...input, title: e.target.value })} /></label></div>
    <label>Source URL <span className="hint">Optional metadata; ProofPath does not scrape this page.</span><input type="url" value={input.sourceUrl ?? ''} maxLength={2048} placeholder="https://…" onChange={e => setInput({ ...input, sourceUrl: e.target.value })} /></label>
    <label>Job description <span className="hint">Required · 100–50,000 characters</span><textarea rows={10} value={input.description} onChange={e => setInput({ ...input, description: e.target.value })} /></label>
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
  return <div className="job-detail"><JobForm title={'Edit ' + (job.title || 'job')} input={input} setInput={setInput} submit={() => update.mutate()} pending={update.isPending} error={update.error?.message} submitLabel="Save changes" />
    {job.status === 'Processing' && <section className="notice" role="status"><strong>Requirement extraction is running.</strong><p>{status.data ? 'Worker state: ' + status.data.state + (status.data.errorCode ? ' · ' + status.data.errorCode : '') : 'Waiting for the worker…'}</p>{status.isError && <ErrorNotice message={status.error.message} retry={() => void status.refetch()} />}{status.data?.state === 'Failed' && status.data.retryable && <button onClick={() => retry.mutate()} disabled={retry.isPending}>Retry extraction</button>}</section>}
    {(job.status === 'ReadyForReview' || job.status === 'Confirmed') && <RequirementReview job={job} refresh={refresh} />}
    {job.status === 'Confirmed' && <MatchPanel jobId={job.id} />}
  </div>
}

function RequirementReview({ job, refresh }: { job: JobSummary; refresh: () => void }) {
  const review = useQuery({ queryKey: ['job-review', job.id, job.descriptionVersion], queryFn: () => jobs.review(job.id) })
  if (review.isPending) return <Loading message="Loading extracted requirements…" />
  if (review.isError) return <ErrorNotice message={review.error.message} retry={() => void review.refetch()} />
  return <ReviewEditor review={review.data} onChanged={() => { void review.refetch(); refresh() }} />
}

export function ReviewEditor({ review, onChanged }: { review: JobReview; onChanged?: () => void }) {
  const [draft, setDraft] = useState<RequirementDraftSet>(review.draft)
  const [acknowledged, setAcknowledged] = useState(false)
  const skills = useQuery({ queryKey: ['job-skills'], queryFn: jobs.skills })
  const locked = Boolean(review.confirmedAt)
  const save = useMutation({ mutationFn: () => jobs.saveReview(review.jobId, review.revision, draft), onSuccess: () => onChanged?.() })
  const confirm = useMutation({ mutationFn: () => jobs.confirm(review.jobId, review.revision), onSuccess: () => onChanged?.() })
  const grouped = useMemo(() => categories.map(category => ({ category, items: draft.requirements.map((item, index) => ({ item, index })).filter(entry => entry.item.category === category) })).filter(group => group.items.length), [draft])
  function change(index: number, value: RequirementDraft) {
    setDraft({ requirements: draft.requirements.map((item, itemIndex) => itemIndex === index ? value : item) }); setAcknowledged(false)
  }
  return <section className="card requirement-review"><div className="row-between"><div><p className="eyebrow">REQUIREMENT REVIEW</p><h2>Version {review.descriptionVersion}</h2></div><span className="badge">{review.confirmedAt ? 'Confirmed set' : 'Review required'}</span></div>
    {review.machine.source.warnings.map(warning => <p className="notice" key={warning}>{warning}</p>)}
    {locked && <p className="notice success" role="status">This requirement set is confirmed and immutable. Edit the job description to create a new version.</p>}
    {!locked && <p className="hint">Required/preferred and importance are separate. “Any of” records alternatives; “all of” records combined requirements. Excluded and unresolved items remain auditable and do not score.</p>}
    {grouped.map(group => <section className="requirement-group" key={group.category}><h3>{labels[group.category]}</h3>{group.category === 'Behavioral' && <p className="hint">Visible for review, but behavioral scoring is disabled.</p>}{group.category === 'Contextual' && <p className="hint">Context is retained without treating it as a candidate requirement.</p>}
      {group.items.map(({ item, index }) => <RequirementRow key={item.key} item={item} index={index} skills={skills.data ?? []} locked={locked} change={change} />)}
    </section>)}
    {!locked && <><label className="review-ack"><input type="checkbox" checked={acknowledged} onChange={e => setAcknowledged(e.target.checked)} />I reviewed the source wording, alternatives, exclusions, and unresolved skills.</label><div className="actions"><button className="secondary" onClick={() => save.mutate()} disabled={save.isPending}>{save.isPending ? 'Saving…' : 'Save review'}</button><button onClick={() => confirm.mutate()} disabled={!acknowledged || confirm.isPending}>{confirm.isPending ? 'Confirming…' : 'Confirm requirement set'}</button></div></>}
    {(save.isError || confirm.isError) && <ErrorNotice message={(save.error ?? confirm.error)!.message} />}
  </section>
}

function RequirementRow({ item, index, skills, locked, change }: { item: RequirementDraft; index: number; skills: SkillOption[]; locked: boolean; change: (index: number, item: RequirementDraft) => void }) {
  const update = (values: Partial<RequirementDraft>) => change(index, { ...item, ...values })
  const unresolved = item.category === 'TechnicalSkill' && !item.skillId
  return <article className={'requirement-row ' + (item.state === 'Excluded' ? 'excluded' : '')}><div className="row-between"><strong>{item.originalWording}</strong><span className={'normalization ' + (unresolved ? 'unresolved' : '')}>{unresolved ? 'Unresolved · not evaluated' : item.normalizationStatus}</span></div>
    <blockquote>“{item.quote}” <cite>{item.sourceBlockId}</cite></blockquote>
    <div className="requirement-controls"><label>Level<select disabled={locked} value={item.level} onChange={e => update({ level: e.target.value as RequirementDraft['level'] })}>{levels.map(value => <option key={value}>{value}</option>)}</select></label><label>Importance<select disabled={locked} value={item.importance} onChange={e => update({ importance: e.target.value as RequirementDraft['importance'] })}>{importance.map(value => <option key={value}>{value}</option>)}</select></label><label>State<select disabled={locked} value={item.state} onChange={e => update({ state: e.target.value as RequirementDraft['state'] })}><option>Extracted</option><option>Excluded</option></select></label></div>
    {item.category === 'TechnicalSkill' && <label>Canonical skill<select disabled={locked} value={item.skillId ?? ''} onChange={e => update({ skillId: e.target.value || null, normalizationStatus: e.target.value ? 'UserConfirmed' : 'Unresolved' })}><option value="">Unresolved / not evaluated</option>{skills.map(skill => <option key={skill.id} value={skill.id}>{skill.displayName}</option>)}</select></label>}
    <div className="requirement-controls group-controls"><label>Relationship<select disabled={locked} value={item.groupType} onChange={e => update({ groupType: e.target.value as RequirementDraft['groupType'], groupKey: e.target.value === 'None' ? null : item.groupKey || 'group-' + (index + 1) })}>{groupTypes.map(value => <option key={value}>{value}</option>)}</select></label>{item.groupType !== 'None' && <label>Group key<input disabled={locked} value={item.groupKey ?? ''} onChange={e => update({ groupKey: e.target.value })} /></label>}</div>
    {item.qualifiers.length > 0 && <p className="qualifiers">Qualifiers: {item.qualifiers.join(' · ')}</p>}
  </article>
}
function MatchPanel({ jobId }: { jobId: string }) {
  const queryClient = useQueryClient()
  const latest = useQuery({ queryKey: ['job-match', jobId], queryFn: () => jobs.latestMatch(jobId) })
  const history = useQuery({ queryKey: ['job-match-history', jobId], queryFn: () => jobs.matches(jobId) })
  const calculate = useMutation({
    mutationFn: () => jobs.calculateMatch(jobId),
    onSuccess: result => {
      queryClient.setQueryData(['job-match', jobId], result)
      void queryClient.invalidateQueries({ queryKey: ['job-match-history', jobId] })
    },
  })
  if (latest.isPending) return <Loading message="Loading latest match…" />
  if (latest.isError) return <ErrorNotice message={latest.error.message} retry={() => void latest.refetch()} />
  return <section className="card match-panel" aria-labelledby="match-title">
    <div className="row-between"><div><p className="eyebrow">DETERMINISTIC MATCH</p><h2 id="match-title">Candidate fit</h2></div><button onClick={() => calculate.mutate()} disabled={calculate.isPending}>{calculate.isPending ? 'Calculating…' : latest.data ? 'Recalculate' : 'Calculate match'}</button></div>
    <p className="hint">Uses confirmed requirements and candidate evidence with matching-v1. AI does not set scores, gaps, or priorities.</p>
    {calculate.isError && <ErrorNotice message={calculate.error.message} />}
    {latest.data ? <MatchResultView match={latest.data} historyCount={history.data?.length ?? 1} /> : <div className="empty-match"><strong>No match calculated yet.</strong><p className="muted">A result is stored as an immutable snapshot so future evidence or requirement changes do not rewrite history.</p></div>}
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
    <div className="match-components">{result.components.map(component => <article key={component.name}><span>{component.name}</span><strong>{component.score === null ? component.status : Math.round(component.score) + '%'}</strong><small>Coverage {percent(component.coverage)}</small></article>)}</div>
    <section><h3>Requirement trace</h3>{result.requirements.map(requirement => <details className={'match-requirement ' + requirement.evaluationStatus.toLowerCase()} key={requirement.requirementId}><summary><span><strong>{requirement.originalWording}</strong><small>{requirement.level} · {requirement.importance}</small></span><span>{requirement.classification ?? requirement.evaluationStatus}{requirement.score === null ? '' : ' · ' + Math.round(requirement.score) + '%'}</span></summary><p>{requirement.details}</p><p className="hint">{requirement.reasonCode} · Confidence {percent(requirement.confidence)}</p>{requirement.evidence.map(item => <blockquote key={item.evidenceId}>“{item.quote || 'Evidence recorded without a quote.'}” <cite>{item.sourceReference} · {item.strength} · contribution {percent(item.contribution)}</cite></blockquote>)}</details>)}</section>
    <section><div className="row-between"><h3>Gaps and verification</h3><span className="hint">{result.gaps.length} identified</span></div>{result.gaps.length === 0 ? <p className="muted">No scored gaps in this snapshot.</p> : <ul className="match-gaps">{result.gaps.map(gap => <li key={gap.requirementId}><strong>{gap.priority}: {gap.requirement}</strong><span>{gap.type} · {gap.reason}</span></li>)}</ul>}</section>
    {result.behavioralAssessment.length > 0 && <section><h3>Behavioral evidence</h3><p className="hint">Reported separately. It never changes the percentage.</p>{result.behavioralAssessment.map(item => <p key={item.themeKey}><strong>{item.themeKey}:</strong> {item.summary}</p>)}</section>}
    <p className="match-history">Result {match.id.slice(0, 8)} · {new Date(match.createdAt).toLocaleString()} · {historyCount} immutable result{historyCount === 1 ? '' : 's'} stored</p>
  </div>
}
