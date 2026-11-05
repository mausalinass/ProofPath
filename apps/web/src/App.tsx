import { useEffect, useRef, useState } from 'react'
import type { MouseEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api/client'
import { navigate, usePath } from './navigation'
import { ErrorNotice, Loading } from './components/Feedback'
import { AuthPage } from './pages/AuthPage'
import { ResumePage } from './pages/ResumePage'
import { GitHubPage } from './pages/GitHubPage'
import { JobsPage } from './pages/JobsPage'
import { ProfilePage } from './pages/ProfilePage'
import { HomePage } from './pages/HomePage'
import { LandingPage } from './pages/LandingPage'
import { SettingsPage } from './pages/SettingsPage'
import { SourcesPage } from './pages/SourcesPage'
import { AccountActionPage } from './pages/AccountActionPage'
import { PrivacyPage } from './pages/PrivacyPage'
import './App.css'

export default function App() {
  const location = usePath(), path = location.split('?')[0]
  const client = useQueryClient()
  const [paused, setPaused] = useState(false)
  const previousLocation = useRef(location)
  useEffect(() => {
    if (previousLocation.current !== location) document.getElementById('workspace-content')?.focus({ preventScroll: true })
    previousLocation.current = location
  }, [location])
  const session = useQuery({ queryKey: ['session'], queryFn: api.session, retry: false })
  const logout = useMutation({ mutationFn: api.logout, onSuccess: () => { client.clear(); navigate('/login') } })
  useEffect(() => { document.title = 'ProofPath · ' + ({ '/': 'Tu trabajo habla por ti', '/home': 'Dossier profesional', '/jobs': 'Opportunity Matcher', '/settings': 'Privacidad', '/sources': 'Tus fuentes', '/login': 'Acceso', '/signup': 'Registro' }[path] ?? 'Tu espacio') }, [path])
  function follow(event: MouseEvent<HTMLDivElement>) {
    const a = (event.target as HTMLElement).closest('a')
    if (!a || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || a.target || a.hasAttribute('download')) return
    const url = new URL(a.href)
    if (url.origin !== window.location.origin || url.hash || url.pathname.startsWith('/api/')) return
    event.preventDefault(); navigate(url.pathname + url.search)
  }
  let page
  const publicPage = ['/', '/login', '/signup', '/privacy', '/verify-email', '/forgot-password', '/reset-password'].includes(path)
  if (path === '/') page = <LandingPage />
  else if (path === '/login' || path === '/signup') page = <AuthPage key={path} signup={path === '/signup'} />
  else if (path === '/privacy') page = <PrivacyPage />
  else if (path === '/verify-email') page = <AccountActionPage action="verify" />
  else if (path === '/forgot-password') page = <AccountActionPage action="forgot" />
  else if (path === '/reset-password') page = <AccountActionPage action="reset" />
  else if (session.isPending) page = <Loading message="Comprobando tu sesión…" />
  else if (session.isError) page = <ErrorNotice message="No pudimos conectar con tu espacio. Comprueba tu conexión y vuelve a intentar." retry={() => void session.refetch()} />
  else if (!session.data) page = <AuthPage signup={false} />
  else if (path === '/settings') page = <SettingsPage session={session.data} />
  else if (path === '/sources') page = <SourcesPage />
  else if (path === '/evidence' || path === '/onboarding/profile') page = <ProfilePage />
  else if (path === '/onboarding/resume') page = <ResumePage key={location} />
  else if (path === '/onboarding/github') page = <GitHubPage />
  else if (path === '/jobs') page = <JobsPage key={location} />
  else if (path === '/home' || path === '/dashboard') page = <HomePage />
  else page = <section className="card"><h1>Página no encontrada.</h1><a href="/home">Volver a tu dossier ↗</a></section>
  const nav = [{ href: '/', label: 'Inicio' }, { href: '/home', label: 'Dashboard' }, { href: '/jobs', label: 'Matcher' }, { href: '/settings', label: 'Privacidad' }]
  return <div className="collage" data-paused={paused} onClick={follow}>
    <a className="skip-link" href="#workspace-content">Saltar al contenido</a>
    <header className="app-nav"><a className="brand" href="/" aria-label="ProofPath, inicio"><span className="brand-mark" aria-hidden="true">✓</span>ProofPath</a><nav aria-label="Main navigation">{nav.map(item => <a key={item.href} href={item.href} aria-current={path === item.href || item.href === '/home' && path === '/dashboard' ? 'page' : undefined}>{item.label}</a>)}</nav><div className="nav-account">{session.data ? <button className="secondary" onClick={() => logout.mutate()} disabled={logout.isPending}>Sign out</button> : <a className="nav-login" href="/login">Entrar ↗</a>}<button className="motion-toggle" aria-pressed={paused} onClick={() => setPaused(!paused)}>{paused ? 'Reanudar movimiento' : 'Pausar movimiento'}</button></div></header>
    <main className="content" id="workspace-content" tabIndex={-1} key={publicPage ? path : 'workspace'}>
      {logout.isError && <ErrorNotice message={logout.error.message} />}
      {!publicPage && session.data && (path === '/sources' || path === '/evidence' || path.startsWith('/onboarding/')) && <nav className="evidence-tabs" aria-label="Evidence sources">{[{ href: '/sources', label: 'Tus fuentes' }, { href: '/onboarding/profile', label: 'Perfil' }, { href: '/onboarding/resume', label: 'Currículum' }, { href: '/onboarding/github', label: 'GitHub' }].map(item => <a key={item.href} href={item.href} aria-current={path === item.href ? 'page' : undefined}>{item.label}</a>)}</nav>}
      {page}
    </main><footer className="app-footer"><strong>ProofPath</strong><span>Tu trabajo, con contexto.</span><a href="/privacy">Aviso de privacidad</a></footer>
  </div>
}
