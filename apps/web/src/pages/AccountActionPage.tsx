import { useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { api } from '../api/client'
import { ErrorNotice } from '../components/Feedback'

export function AccountActionPage({ action }: { action: 'verify' | 'forgot' | 'reset' }) {
  const query = new URLSearchParams(window.location.search)
  const [email, setEmail] = useState(query.get('email') ?? '')
  const [password, setPassword] = useState('')
  const token = query.get('token') ?? ''
  const mutation = useMutation({ mutationFn: async () => {
    if (action === 'verify') await api.confirmEmail(email, token)
    else if (action === 'forgot') await api.forgotPassword(email)
    else await api.resetPassword(email, token, password)
  } })
  function submit(event: FormEvent) { event.preventDefault(); mutation.mutate() }
  const title = action === 'verify' ? 'Verify your email' : action === 'forgot' ? 'Reset your password' : 'Choose a new password'
  return <main className="auth"><a className="brand" href="/">ProofPath<span>Evidence for your next step.</span></a>
    <section className="card form-card"><h1>{title}</h1>
      {mutation.isSuccess ? <div role="status"><p>{action === 'forgot' ? 'If that verified account exists, a reset link has been sent.' : 'Done. You can now sign in.'}</p><a href="/login">Return to sign in</a></div> : <form onSubmit={submit}>
        <label>Email<input type="email" autoComplete="email" required value={email} readOnly={action !== 'forgot'} onChange={event => setEmail(event.target.value)} /></label>
        {action === 'reset' && <><label>New password<input type="password" autoComplete="new-password" required minLength={12} value={password} onChange={event => setPassword(event.target.value)} /></label><p className="hint">Use at least 12 characters with uppercase, lowercase, a number and a symbol.</p></>}
        {mutation.isError && <ErrorNotice message={mutation.error.message} />}
        <button disabled={mutation.isPending || action !== 'forgot' && !token}>{mutation.isPending ? 'Please wait…' : action === 'verify' ? 'Verify email' : action === 'forgot' ? 'Send reset link' : 'Reset password'}</button>
      </form>}
    </section></main>
}
