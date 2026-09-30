import { useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { Profile, ProfileInput } from '../api/client'
import { ErrorNotice, Loading } from '../components/Feedback'

export function ProfilePage() {
  const query = useQuery({ queryKey: ['profile'], queryFn: api.profile })
  if (query.isPending) return <Loading message="Loading your professional profile…" />
  if (query.isError) return <ErrorNotice message={query.error.message} retry={() => void query.refetch()} />
  return <ProfileForm initial={query.data} />
}

function ProfileForm({ initial }: { initial: Profile | null }) {
  const client = useQueryClient()
  const [values, setValues] = useState<ProfileInput>({
    firstName: initial?.firstName ?? '', lastName: initial?.lastName ?? '', headline: initial?.headline ?? '',
    location: initial?.location ?? '', workAuthorization: initial?.workAuthorization ?? '', educationSummary: initial?.educationSummary ?? '',
  })
  const mutation = useMutation({ mutationFn: () => api.saveProfile(values), onSuccess: profile => {
    client.setQueryData(['profile'], profile); void client.invalidateQueries({ queryKey: ['home'] })
  } })
  function submit(event: FormEvent) { event.preventDefault(); mutation.mutate() }
  return <><div className="page-heading"><p className="eyebrow">MY EVIDENCE / PROFESSIONAL PROFILE</p><h1>Your story starts here.</h1><p className="muted">Keep your professional facts accurate. You can update them at any time.</p></div>
    <section className="card form-card"><h2>{initial ? 'Edit your profile' : 'Set up your profile'}</h2>
      <form onSubmit={submit}>
        <div className="form-grid">{(['firstName', 'lastName', 'headline', 'location', 'workAuthorization'] as const).map(field => {
          const labels = { firstName: 'First name', lastName: 'Last name', headline: 'Headline', location: 'Location', workAuthorization: 'Work authorization' }
          return <label key={field}>{labels[field]}<input value={values[field] ?? ''} onChange={e => { mutation.reset(); setValues({ ...values, [field]: e.target.value }) }} /></label>
        })}</div>
        <label>Education summary<textarea rows={4} value={values.educationSummary ?? ''} onChange={e => { mutation.reset(); setValues({ ...values, educationSummary: e.target.value }) }} /></label>
        <p className="hint">Work authorization is a contextual fact. It does not establish a technical skill.</p>
        {mutation.isError && <ErrorNotice message={mutation.error.message} />}
        {mutation.isSuccess && <p className="notice success" role="status">Profile saved. Your changes are private and persistent.</p>}
        <button disabled={mutation.isPending}>{mutation.isPending ? 'Saving…' : 'Save profile'}</button>
      </form>
    </section>
  </>
}
