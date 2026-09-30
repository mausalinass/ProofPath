import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { resumes } from '../api/resumes'
import type { ResumeDraft, ResumeReview, ResumeSummary, SourceBlock, FactDraft } from '../api/resumes'
import { ErrorNotice, Loading } from '../components/Feedback'
import './ResumePage.css'

const themes = ['COLLABORATION', 'CROSS_FUNCTIONAL_COLLABORATION', 'COMMUNICATION', 'STAKEHOLDER_COMMUNICATION', 'OWNERSHIP', 'LEADERSHIP', 'INITIATIVE', 'ADAPTABILITY', 'PROBLEM_SOLVING', 'MENTORING']
const contexts = ['SkillsSection', 'ExperienceStatement', 'ProjectStatement', 'Education', 'Certification', 'Other']
const label = (text: string) => text.replaceAll('_', ' ').replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase()
const errors: Record<string, string> = {
  PROVIDER_NOT_CONFIGURED: 'Automatic extraction is waiting for the AI service to be configured. Your private file is saved.',
  PROVIDER_CONFIGURATION_ERROR: 'The AI service configuration needs attention. Your private file is saved.',
  PROVIDER_UNAVAILABLE: 'The AI service is temporarily unavailable. Your private file is saved.',
  PROVIDER_TIMEOUT: 'The AI service took too long. You can retry without uploading again.',
  NO_EXTRACTABLE_TEXT: 'This document has no readable text. Upload a text-based PDF or DOCX; scanned images cannot be read yet.',
  DOCUMENT_TEXT_TOO_LONG: 'This résumé is too long to analyze reliably. Upload a shorter version; nothing was silently discarded.',
  INVALID_EXTRACTION: 'The extracted facts did not pass verification. Your existing confirmed facts are unchanged.',
  EXTRACTION_INCOMPLETE: 'The extraction was incomplete. Upload a shorter résumé or contact the workspace administrator.',
}

export function ResumePage() {
  const client = useQueryClient()
  const fileInput = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [selection, setSelection] = useState<string | null>(null)
  const [fileError, setFileError] = useState('')
  const list = useQuery({ queryKey: ['resumes'], queryFn: resumes.list })
  const upload = useMutation({ mutationFn: (value: File) => resumes.upload(value), onSuccess: result => {
    if (fileInput.current) fileInput.current.value = ''
    setSelection(result.id); setFile(null); void client.invalidateQueries({ queryKey: ['resumes'] })
  } })
  if (list.isPending) return <Loading message="Loading your résumés…" />
  if (list.isError) return <ErrorNotice message={list.error.message} retry={() => void list.refetch()} />
  const selected = list.data.find(item => item.id === selection) ?? list.data[0]
  return <section className="resume-workspace" aria-labelledby="resume-heading">
    <div className="page-heading"><p className="eyebrow">RÉSUMÉ INTELLIGENCE</p><h2 id="resume-heading">Your experience, with the source attached.</h2><p className="muted">Upload, review and confirm. Only the facts you confirm become active evidence.</p></div>
    <div className="card"><h3>Add a résumé</h3><p className="hint">Private PDF or DOCX · up to 10 MB · text-based documents only</p>
      <form onSubmit={event => { event.preventDefault(); if (file) upload.mutate(file) }}>
        <label>Résumé file<input ref={fileInput} type="file" accept=".pdf,.docx" disabled={upload.isPending} onChange={event => {
          const picked = event.target.files?.[0]; upload.reset(); setFileError('')
          if (picked && (picked.size > 10 * 1024 * 1024 || !/\.(pdf|docx)$/i.test(picked.name))) { setFile(null); setFileError('Choose a PDF or DOCX smaller than 10 MB.'); return }
          setFile(picked ?? null)
        }} /></label>
        {fileError && <ErrorNotice message={fileError} />}{upload.isError && <ErrorNotice message={upload.error.message} />}
        <button disabled={!file || upload.isPending}>{upload.isPending ? 'Uploading privately…' : 'Upload résumé'}</button>
      </form><p className="hint resume-policy">Confirming a new version replaces the active résumé facts. Earlier versions remain in your private history until you delete your account.</p>
    </div>
    {list.data.length === 0 ? <div className="card"><h3>No résumé yet</h3><p className="muted">Start with your latest résumé. You will be able to correct the extracted facts before confirming them.</p></div> :
      <div className="resume-layout"><aside className="card resume-versions" aria-label="Résumé versions"><h3>Your versions</h3>{list.data.map(item => <button key={item.id} className="secondary version-button" aria-pressed={selected?.id === item.id} onClick={() => setSelection(item.id)}>
        <strong>Version {item.version}{item.active ? ' · Active' : ''}</strong><span>{item.fileName}</span><small>{new Date(item.createdAt).toLocaleDateString()}{item.confirmedAt ? ' · Confirmed' : ''}</small>
      </button>)}</aside><div>{selected && <ResumeVersion key={selected.id} resume={selected} />}</div></div>}
  </section>
}

function ResumeVersion({ resume }: { resume: ResumeSummary }) {
  const client = useQueryClient()
  const job = useQuery({ queryKey: ['analysis', resume.analysisJobId], queryFn: () => resumes.status(resume.analysisJobId!), enabled: !!resume.analysisJobId,
    refetchInterval: query => query.state.data && ['Completed', 'PartiallyCompleted', 'Failed', 'Cancelled'].includes(query.state.data.state) ? false : 2000 })
  const done = job.data?.state === 'Completed' || job.data?.state === 'PartiallyCompleted' || !!resume.confirmedAt
  const review = useQuery({ queryKey: ['resume-review', resume.id], queryFn: () => resumes.review(resume.id), enabled: done, retry: false })
  const action = useMutation({ mutationFn: (kind: 'retry' | 'cancel') => resumes[kind](resume.analysisJobId!), onSuccess: () => { void client.invalidateQueries({ queryKey: ['analysis', resume.analysisJobId] }) } })
  useEffect(() => { if (done) void client.invalidateQueries({ queryKey: ['resumes'] }) }, [done, client])
  return <><div className="card"><div className="resume-title"><h3>Version {resume.version}</h3>{resume.status !== 'UploadFailed' && resume.status !== 'Uploading' && <a href={resumes.downloadUrl(resume.id)}>Download original</a>}</div>
    {!resume.analysisJobId && <p role="alert">The upload did not complete. Upload the file again.</p>}
    {job.isError && <ErrorNotice message={job.error.message} retry={() => void job.refetch()} />}
    {job.data && !done && <><p role="status">{job.data.state === 'Pending' ? 'Waiting for analysis…' : job.data.state === 'Processing' ? 'Reading your résumé…' : job.data.state === 'Cancelled' ? 'Analysis cancelled. The private file remains available.' : 'Analysis needs attention.'}</p>
      {job.data.errorCode && <p className="notice">{errors[job.data.errorCode] ?? 'Analysis could not complete. Your file is saved; review the issue before retrying.'}</p>}
      {['Pending', 'Processing'].includes(job.data.state) && <button className="secondary" disabled={action.isPending} onClick={() => action.mutate('cancel')}>Cancel analysis</button>}
      {job.data.state === 'Failed' && job.data.retryable && <button disabled={action.isPending} onClick={() => action.mutate('retry')}>Retry analysis</button>}
    </>}{action.isError && <ErrorNotice message={action.error.message} />}
    {done && review.isPending && <Loading message="Loading extracted facts…" />}
    {done && review.isError && <ErrorNotice message={review.error.message} retry={() => void review.refetch()} />}
    </div>{review.data && <ReviewEditor key={`${review.data.resumeId}:${review.data.revision}:${review.data.confirmedAt}`} review={review.data} />}</>
}

function Citation({ item, blocks, change, disabled }: { item: { sourceBlockId: string; quote: string }; blocks: SourceBlock[]; change: (value: { sourceBlockId: string; quote: string }) => void; disabled: boolean }) {
  const block = blocks.find(value => value.id === item.sourceBlockId)
  return <details className="source-citation"><summary>Source citation{block?.page ? ` · page ${block.page}` : ''}</summary>
    <label>Source passage<select disabled={disabled} value={item.sourceBlockId} onChange={event => { const value = blocks.find(b => b.id === event.target.value)!; change({ sourceBlockId: value.id, quote: value.text.slice(0, 500) }) }}>{blocks.map(value => <option key={value.id} value={value.id}>{value.page ? `Page ${value.page}` : value.text.slice(0, 80)}</option>)}</select></label>
    <label>Exact supporting quote<textarea disabled={disabled} value={item.quote} onChange={event => change({ sourceBlockId: item.sourceBlockId, quote: event.target.value })} /></label>
    <blockquote>{block?.text}</blockquote></details>
}

export function ReviewEditor({ review }: { review: ResumeReview }) {
  const client = useQueryClient(); const [draft, setDraft] = useState<ResumeDraft>(review.draft); const [dirty, setDirty] = useState(false)
  const [acknowledged, setAcknowledged] = useState(false)
  const locked = !!review.confirmedAt
  const save = useMutation({ mutationFn: () => resumes.save(review.resumeId, review.revision, draft), onSuccess: value => { client.setQueryData(['resume-review', review.resumeId], value); setDirty(false) } })
  const confirm = useMutation({ mutationFn: () => resumes.confirm(review.resumeId, review.revision), onSuccess: () => {
    void client.invalidateQueries({ queryKey: ['resume-review', review.resumeId] }); void client.invalidateQueries({ queryKey: ['resumes'] }); void client.invalidateQueries({ queryKey: ['home'] })
  } })
  const busy = save.isPending || confirm.isPending
  function update(value: ResumeDraft) { setDraft(value); setDirty(true); setAcknowledged(false); save.reset(); confirm.reset() }
  function fact(index: number, patch: Partial<FactDraft>) { update({ ...draft, facts: draft.facts.map((value, i) => i === index ? { ...value, ...patch } : value) }) }
  const first = review.machine.source.blocks[0]
  return <section className="card" aria-label="Review extracted résumé"><h3>{locked ? 'Confirmed facts' : 'Review your extracted facts'}</h3>
    {locked ? <p className="notice success" role="status">{review.active ? 'This résumé supplies your active facts.' : 'Historical version. A newer confirmation supplies your active facts.'}</p> : <p className="notice">Check names, dates and source citations. Unknown dates can stay empty. These facts are not active until you confirm them.</p>}
    {review.machine.source.warnings.map(warning => <p className="notice" key={warning}>{warning}</p>)}
    <fieldset disabled={locked || busy}><legend>Experience, education, projects and credentials</legend>
      {draft.facts.map((item, index) => <article className="fact-card" key={index}><div className="form-grid">
        <label>Fact type<select value={item.kind} onChange={event => fact(index, { kind: event.target.value as FactDraft['kind'] })}>{['Experience', 'Education', 'Project', 'Credential'].map(kind => <option key={kind}>{kind}</option>)}</select></label>
        <label>Name / role / degree<input value={item.name} onChange={event => fact(index, { name: event.target.value })} /></label>
        <label>Organization / institution<input value={item.organization ?? ''} onChange={event => fact(index, { organization: event.target.value || null })} /></label>
        <label>Status, if stated<input value={item.status ?? ''} onChange={event => fact(index, { status: event.target.value || null })} /></label>
        <label>Start date, as stated<input value={item.startDateText ?? ''} onChange={event => fact(index, { startDateText: event.target.value || null })} /></label>
        <label>End / expected date, as stated<input value={item.endDateText ?? ''} onChange={event => fact(index, { endDateText: event.target.value || null })} /></label>
      </div><label>Responsibilities / details<textarea value={item.detail ?? ''} onChange={event => fact(index, { detail: event.target.value || null })} /></label>
        <Citation item={item} blocks={review.machine.source.blocks} disabled={locked || busy} change={patch => fact(index, patch)} />
        {!locked && <button className="secondary" onClick={() => update({ ...draft, facts: draft.facts.filter((_, i) => i !== index) })}>Remove fact {index + 1}</button>}
      </article>)}
      {!locked && first && <button className="secondary" onClick={() => update({ ...draft, facts: [...draft.facts, { kind: 'Experience', name: '', organization: null, detail: null, startDateText: null, endDateText: null, status: null, sourceBlockId: first.id, quote: first.text.slice(0, 500) }] })}>Add missing fact</button>}
    </fieldset>
    <fieldset disabled={locked || busy}><legend>Technical skill mentions</legend><p className="hint">A résumé mention is a self-reported claim; repetition does not establish stronger proof.</p>
      {draft.skills.map((item, index) => <article className="fact-card" key={index}><label>Skill term<input value={item.term} onChange={event => update({ ...draft, skills: draft.skills.map((value, i) => i === index ? { ...value, term: event.target.value } : value) })} /></label>
        <label>Where it was mentioned<select value={item.context} onChange={event => update({ ...draft, skills: draft.skills.map((value, i) => i === index ? { ...value, context: event.target.value } : value) })}>{contexts.map(context => <option key={context} value={context}>{label(context)}</option>)}</select></label>
        <Citation item={item} blocks={review.machine.source.blocks} disabled={locked || busy} change={patch => update({ ...draft, skills: draft.skills.map((value, i) => i === index ? { ...value, ...patch } : value) })} />
        {!locked && <button className="secondary" onClick={() => update({ ...draft, skills: draft.skills.filter((_, i) => i !== index) })}>Remove skill {index + 1}</button>}
      </article>)}
      {!locked && first && <button className="secondary" onClick={() => update({ ...draft, skills: [...draft.skills, { term: '', context: 'Other', sourceBlockId: first.id, quote: first.text.slice(0, 500) }] })}>Add missing skill</button>}
    </fieldset>
    <fieldset disabled={locked || busy}><legend>Professional behavior statements</legend><p className="hint">These statements are shown separately. They do not affect matching scores or establish personality traits.</p>
      {draft.behaviors.length === 0 && <p className="muted">No supported statements were extracted.</p>}
      {draft.behaviors.map((item, index) => <article className="fact-card" key={index}><label>Theme<select value={item.themeKey} onChange={event => update({ ...draft, behaviors: draft.behaviors.map((value, i) => i === index ? { ...value, themeKey: event.target.value } : value) })}>{themes.map(theme => <option key={theme} value={theme}>{label(theme)}</option>)}</select></label>
        <label>Professional statement<textarea value={item.statement} onChange={event => update({ ...draft, behaviors: draft.behaviors.map((value, i) => i === index ? { ...value, statement: event.target.value } : value) })} /></label>
        <Citation item={item} blocks={review.machine.source.blocks} disabled={locked || busy} change={patch => update({ ...draft, behaviors: draft.behaviors.map((value, i) => i === index ? { ...value, ...patch } : value) })} />
        {!locked && <button className="secondary" onClick={() => update({ ...draft, behaviors: draft.behaviors.filter((_, i) => i !== index) })}>Remove statement {index + 1}</button>}
      </article>)}
    </fieldset>
    {!locked && <><div className="actions"><button disabled={!dirty || busy} onClick={() => save.mutate()}>{save.isPending ? 'Saving…' : 'Save corrections'}</button></div>
      {save.isError && <ErrorNotice message={save.error.message} />}{save.isSuccess && <p role="status">Corrections saved.</p>}
      <label className="confirm-check"><input type="checkbox" checked={acknowledged} disabled={dirty || busy} onChange={event => setAcknowledged(event.target.checked)} />I reviewed these facts. Confirming replaces my active résumé facts and preserves older versions.</label>
      {dirty && <p className="hint">Save your corrections before confirming.</p>}
      <button disabled={dirty || !acknowledged || busy} onClick={() => confirm.mutate()}>{confirm.isPending ? 'Confirming…' : 'Confirm résumé facts'}</button>
      {confirm.isError && <ErrorNotice message={confirm.error.message} />}
    </>}
  </section>
}
