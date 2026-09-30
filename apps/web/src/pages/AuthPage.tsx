import { useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { ErrorNotice } from '../components/Feedback'

export function AuthPage({ signup }: { signup: boolean }) {
  const queryClient = useQueryClient()
  const [values, setValues] = useState({ firstName: '', lastName: '', email: '', password: '' })
  const [registeredEmail, setRegisteredEmail] = useState<string | null>(null)
  const mutation = useMutation({ mutationFn: async () => {
    if (signup && registeredEmail !== values.email) {
      await api.register(values); setRegisteredEmail(values.email)
    }
    return await api.login(values.email, values.password)
  }, onSuccess: () => { queryClient.clear(); window.location.assign('/home') } })
  function submit(event: FormEvent) { event.preventDefault(); mutation.mutate() }
  return <main className="auth"><a className="brand" href="/">ProofPath<span>Evidence for your next step.</span></a>
    <section className="card form-card"><p className="eyebrow">YOUR CANDIDATE WORKSPACE</p>
      <h1>{signup ? 'Start with what you can prove.' : 'Welcome back.'}</h1>
      <p className="muted">{signup ? 'Create your account and build your professional profile.' : 'Sign in to your private ProofPath workspace.'}</p>
      <form onSubmit={submit}>
        {signup && <div className="form-grid">{(['firstName', 'lastName'] as const).map(field => <label key={field}>{field === 'firstName' ? 'First name' : 'Last name (optional)'}
          <input autoComplete={field === 'firstName' ? 'given-name' : 'family-name'} required={field === 'firstName'} value={values[field]} onChange={e => setValues({ ...values, [field]: e.target.value })} />
        </label>)}</div>}
        <label>Email<input type="email" autoComplete="email" required value={values.email} onChange={e => setValues({ ...values, email: e.target.value })} /></label>
        <label>Password<input type="password" autoComplete={signup ? 'new-password' : 'current-password'} required value={values.password} onChange={e => setValues({ ...values, password: e.target.value })} /></label>
        {signup && <p className="hint">Use at least 6 characters with uppercase, lowercase, a number and a symbol. Your profile is private.</p>}
        {mutation.isError && <ErrorNotice message={mutation.error.message} />}
        <button disabled={mutation.isPending}>{mutation.isPending ? 'Please wait…' : signup ? 'Create account' : 'Sign in'}</button>
      </form>
      <p className="auth-switch">{signup ? 'Already have an account?' : 'New to ProofPath?'} <a href={signup ? '/login' : '/signup'}>{signup ? 'Sign in' : 'Create account'}</a></p>
    </section><p className="footnote">Your evidence stays under your control.</p>
  </main>
}
