import { useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { ErrorNotice } from '../components/Feedback'

export function AuthPage({ signup }: { signup: boolean }) {
  const queryClient = useQueryClient()
  const [values, setValues] = useState({ firstName: '', lastName: '', email: '', password: '', acceptPrivacy: false, privacyVersion: '2026-10-30' })
  const [registeredEmail, setRegisteredEmail] = useState<string | null>(null)
  const mutation = useMutation({ mutationFn: async () => {
    if (signup && registeredEmail !== values.email) {
      const registration = await api.register(values); setRegisteredEmail(values.email)
      if (registration.requiresEmailConfirmation) return null
    }
    return await api.login(values.email, values.password)
  }, onSuccess: session => { if (session) { queryClient.clear(); window.location.assign('/home') } } })
  function submit(event: FormEvent) { event.preventDefault(); mutation.mutate() }
  return <main className="auth"><a className="brand" href="/">ProofPath<span>Evidence for your next step.</span></a>
    <section className="card form-card"><p className="eyebrow">YOUR CANDIDATE WORKSPACE</p>
      <h1>{signup ? 'Start with what you can prove.' : 'Welcome back.'}</h1>
      <p className="muted">{signup ? 'Create your account and build your professional profile.' : 'Sign in to your private ProofPath workspace.'}</p>
      {signup && registeredEmail && !mutation.isPending && !mutation.isError ? <div role="status"><p>Check your email to verify the account before signing in.</p><a href="/login">Return to sign in</a></div> : <form onSubmit={submit}>
        {signup && <div className="form-grid">{(['firstName', 'lastName'] as const).map(field => <label key={field}>{field === 'firstName' ? 'First name' : 'Last name (optional)'}
          <input autoComplete={field === 'firstName' ? 'given-name' : 'family-name'} required={field === 'firstName'} value={values[field]} onChange={e => setValues({ ...values, [field]: e.target.value })} />
        </label>)}</div>}
        <label>Email<input type="email" autoComplete="email" required value={values.email} onChange={e => setValues({ ...values, email: e.target.value })} /></label>
        <label>Password<input type="password" autoComplete={signup ? 'new-password' : 'current-password'} required value={values.password} onChange={e => setValues({ ...values, password: e.target.value })} /></label>
        {signup && <p className="hint">Use at least 12 characters with uppercase, lowercase, a number and a symbol. Your profile is private.</p>}
        {signup && <label className="confirm-check"><input type="checkbox" required checked={values.acceptPrivacy} onChange={e => setValues({ ...values, acceptPrivacy: e.target.checked })} />I have read and accept the <a href="/privacy" target="_blank" rel="noreferrer">Privacy Notice</a>.</label>}
        {mutation.isError && <ErrorNotice message={mutation.error.message} />}
        <button disabled={mutation.isPending}>{mutation.isPending ? 'Please wait…' : signup ? 'Create account' : 'Sign in'}</button>
      </form>}
      {!signup && <p className="auth-switch"><a href="/forgot-password">Forgot your password?</a></p>}
      <p className="auth-switch">{signup ? 'Already have an account?' : 'Need access?'} <a href={signup ? '/login' : '/privacy'}>{signup ? 'Sign in' : 'Review privacy and contact the ProofPath owner.'}</a></p>
    </section><p className="footnote">Your evidence stays under your control.</p>
  </main>
}
