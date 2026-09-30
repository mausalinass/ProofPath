import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { Session } from '../api/client'
import { ErrorNotice } from '../components/Feedback'

export function SettingsPage({ session }: { session: Session }) {
  const client = useQueryClient()
  const [confirm, setConfirm] = useState(false)
  const [password, setPassword] = useState('')
  const deletion = useMutation({ mutationFn: () => api.deleteAccount(password), onSuccess: () => { client.clear(); window.location.assign('/login') } })
  return <><div className="page-heading"><p className="eyebrow">ACCOUNT & PRIVACY</p><h1>Your account. Your control.</h1><p className="muted">Manage your private workspace and personal data.</p></div>
    <section className="card"><h2>Account</h2><p>{session.email}</p><a href="/onboarding/profile">Edit professional profile</a></section>
    <section className="card"><h2>Connected sources</h2><p className="muted">Manage private résumé versions and your read-only GitHub connection in My Evidence.</p><a href="/onboarding/github">Manage GitHub connection</a></section>
    <section className="card danger-zone"><h2>Delete account and data</h2><p>This permanently removes your account, profile, résumés and extracted facts. Private file cleanup continues automatically if storage is temporarily unavailable. This action cannot be undone.</p>
      {!confirm ? <button className="danger" onClick={() => setConfirm(true)}>Delete my account</button> : <form onSubmit={e => { e.preventDefault(); deletion.mutate() }}>
        <p role="alert">Confirm permanent deletion by entering your current password.</p><label>Current password<input type="password" required autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} /></label>
        {deletion.isError && <ErrorNotice message={deletion.error.message} />}
        <div className="actions"><button className="danger" disabled={deletion.isPending}>{deletion.isPending ? 'Deleting…' : 'Permanently delete account'}</button><button type="button" className="secondary" disabled={deletion.isPending} onClick={() => { setConfirm(false); setPassword(''); deletion.reset() }}>Cancel</button></div>
      </form>}
    </section>
  </>
}
