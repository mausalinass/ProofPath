import { useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { ErrorNotice } from '../components/Feedback'
import { CollageArt } from '../components/CollageArt'
import { navigate } from '../navigation'

export function AuthPage({ signup }: { signup: boolean }) {
  const queryClient = useQueryClient()
  const [values, setValues] = useState({ firstName: '', lastName: '', email: '', password: '', acceptPrivacy: false, privacyVersion: '2026-10-30' })
  const [registeredEmail, setRegisteredEmail] = useState<string | null>(null)
  const [visible, setVisible] = useState(false)
  const [confirmation, setConfirmation] = useState('')
  const mutation = useMutation({ mutationFn: async () => {
    if (signup && registeredEmail !== values.email) {
      const registration = await api.register(values); setRegisteredEmail(values.email)
      if (registration.requiresEmailConfirmation) return null
    }
    return await api.login(values.email, values.password)
  }, onSuccess: session => { if (session) { queryClient.clear(); navigate(signup ? '/sources' : '/home') } } })
  function submit(event: FormEvent) { event.preventDefault(); if (signup && confirmation !== values.password) { const field = (event.currentTarget as HTMLFormElement).elements.namedItem('confirmPassword') as HTMLInputElement; field.setCustomValidity('Las contraseñas no coinciden.'); field.reportValidity(); return } mutation.mutate() }
  return <section className="auth"><a className="brand" href="/">ProofPath<span>Evidence for your next step.</span></a>
    <div className="auth-layout"><section className="card form-card">
      <h1>{signup ? <>Empieza<br />por ti.</> : <>Vuelve a<br />tu camino.</>}</h1>
      <p className="muted">{signup ? 'Tu próxima oportunidad empieza con lo que ya construiste.' : 'Entra a tu espacio y sigue construyendo tu próxima oportunidad.'}</p>
      {signup && registeredEmail && !mutation.isPending && !mutation.isError ? <div role="status"><p>Check your email to verify the account before signing in.</p><a href="/login">Return to sign in</a></div> : <form onSubmit={submit}>
        {signup && <div className="form-grid">{(['firstName', 'lastName'] as const).map(field => <label key={field}>{field === 'firstName' ? 'First name' : 'Last name (optional)'}
          <input name={field} autoComplete={field === 'firstName' ? 'given-name' : 'family-name'} required={field === 'firstName'} value={values[field]} onChange={e => setValues({ ...values, [field]: e.target.value })} />
        </label>)}</div>}
        <label>Email<input name="email" type="email" autoComplete="email" spellCheck={false} required value={values.email} onChange={e => setValues({ ...values, email: e.target.value })} /></label>
        <div><label htmlFor="account-password">Password</label><div className="password-field"><input id="account-password" name="password" type={visible ? 'text' : 'password'} autoComplete={signup ? 'new-password' : 'current-password'} required value={values.password} onChange={e => { setValues({ ...values, password: e.target.value }); const field = e.target.form?.elements.namedItem('confirmPassword') as HTMLInputElement | null; field?.setCustomValidity('') }} /><button type="button" aria-pressed={visible} onClick={() => setVisible(!visible)}>{visible ? 'Ocultar' : 'Mostrar'}</button></div></div>
        {signup && <label style={{marginTop:20}}>Confirm password<input name="confirmPassword" type={visible ? 'text' : 'password'} autoComplete="new-password" required value={confirmation} onChange={e => { setConfirmation(e.target.value); e.target.setCustomValidity(e.target.value === values.password ? '' : 'Las contraseñas no coinciden.') }} onInput={e => e.currentTarget.setCustomValidity('')} onBlur={e => e.currentTarget.setCustomValidity(confirmation === values.password ? '' : 'Las contraseñas no coinciden.')} /></label>}
        {signup && <p className="hint">Use at least 12 characters with uppercase, lowercase, a number and a symbol. Your profile is private.</p>}
        {signup && <label className="confirm-check"><input type="checkbox" required checked={values.acceptPrivacy} onChange={e => setValues({ ...values, acceptPrivacy: e.target.checked })} />I have read and accept the <a href="/privacy" target="_blank" rel="noreferrer">Privacy Notice</a>.</label>}
        {mutation.isError && <ErrorNotice message={mutation.error.message} />}
        <button disabled={mutation.isPending}>{mutation.isPending ? 'Please wait…' : signup ? 'Create account' : 'Sign in'}</button>
      </form>}
      {!signup && <p className="auth-switch"><a href="/forgot-password">Forgot your password?</a></p>}
      <p className="auth-switch">{signup ? '¿Ya tienes una cuenta?' : '¿Es tu primera vez?'} <a href={signup ? '/login' : import.meta.env.DEV || import.meta.env.VITE_REGISTRATION_ENABLED === 'true' ? '/signup' : '/privacy'}>{signup ? 'Sign in' : import.meta.env.DEV || import.meta.env.VITE_REGISTRATION_ENABLED === 'true' ? 'Crear cuenta' : 'Consultar acceso y privacidad'}</a></p>
    </section><aside className="auth-art"><h2>{signup ? <>Tu historia<br />tiene futuro.</> : <>Lo que hiciste.<br />Lo que viene.</>}</h2><CollageArt kind="evidence" /><p>Primero, tu cuenta. Después, tus proyectos y tu currículum. Un paso a la vez.</p></aside></div><p className="footnote">Tu trabajo, con contexto. Tu información, bajo tu control.</p>
  </section>
}
