import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api/client'
import { ErrorNotice, Loading } from './components/Feedback'
import { AuthPage } from './pages/AuthPage'
import { ResumePage } from './pages/ResumePage'
import { GitHubPage } from './pages/GitHubPage'
import { JobsPage } from './pages/JobsPage'
import { ProfilePage } from './pages/ProfilePage'
import { HomePage } from './pages/HomePage'
import { SettingsPage } from './pages/SettingsPage'
import './App.css'

export default function App() {
  const path = window.location.pathname
  const client = useQueryClient()
  const session = useQuery({ queryKey: ['session'], queryFn: api.session, retry: false })
  const logout = useMutation({ mutationFn: api.logout, onSuccess: () => { client.clear(); window.location.assign('/login') } })
  if (session.isPending) return <Loading message="Checking your session…" />
  if (session.isError) return <main className="auth"><ErrorNotice message="Your workspace could not be reached. Check your connection and retry." retry={() => void session.refetch()} /></main>
  if (!session.data) return <AuthPage signup={path === '/signup'} />
  const nav = [{ href: '/home', label: 'Home' }, { href: '/evidence', label: 'My Evidence' }, { href: '/jobs', label: 'Jobs' }, { href: '/settings', label: 'Settings' }]
  let page
  if (path === '/settings') page = <SettingsPage session={session.data} />
  else if (path === '/evidence') page = <><ProfilePage /><ResumePage /><GitHubPage /></>
  else if (path === '/onboarding/profile') page = <ProfilePage />
  else if (path === '/onboarding/resume') page = <ResumePage />
  else if (path === '/onboarding/github') page = <GitHubPage />
  else if (path === '/jobs') page = <JobsPage />
  else page = <HomePage />
  return <div className="app-shell"><aside className="sidebar"><a className="brand" href="/home">ProofPath<span>Evidence for your next step.</span></a><nav aria-label="Main navigation">{nav.map(item => <a key={item.href} href={item.href} aria-current={path === item.href || item.href === '/evidence' && path === '/onboarding/profile' ? 'page' : undefined}>{item.label}</a>)}</nav><div className="sidebar-bottom"><p className="hint">Private candidate workspace</p><button className="secondary" onClick={() => logout.mutate()} disabled={logout.isPending}>Sign out</button></div></aside>
    <div className="workspace"><header className="topbar"><span>Candidate workspace</span><span className="account-email">{session.data.email}</span></header><main className="content">{logout.isError && <ErrorNotice message={logout.error.message} />}{page}</main><footer>ProofPath · Evidence over claims</footer></div>
  </div>
}
