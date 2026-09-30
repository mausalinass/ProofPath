import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import { ErrorNotice, Loading } from '../components/Feedback'

export function HomePage() {
  const query = useQuery({ queryKey: ['home'], queryFn: api.home })
  if (query.isPending) return <Loading />
  if (query.isError) return <ErrorNotice message={query.error.message} retry={() => void query.refetch()} />
  const { profile, profileComplete } = query.data
  return <><div className="page-heading"><p className="eyebrow">YOUR WORKSPACE</p><h1>{profile?.firstName ? `Welcome, ${profile.firstName}.` : 'Welcome to ProofPath.'}</h1><p className="muted">Professional facts today. Traceable evidence for what comes next.</p></div>
    <section className="card hero-card"><span className="badge">{profileComplete ? 'PROFILE READY' : 'START HERE'}</span><h2>{profileComplete ? 'Your professional profile is ready.' : 'Tell us about your professional background.'}</h2>
      <p>Review your profile, résumé and selected code evidence before analyzing opportunities.</p><a className="button" href="/onboarding/profile">{profileComplete ? 'Review profile' : 'Complete profile'}</a>
    </section>
    <div className="two-columns"><section className="card"><p className="eyebrow">SETUP PROGRESS</p><ol className="steps"><li><span className={profileComplete ? 'step done' : 'step'}>{profileComplete ? '✓' : '1'}</span><div><strong>Professional profile</strong><p className="muted">{profileComplete ? 'Saved in your private workspace' : 'Add your basic professional facts'}</p></div></li><li><span className="step">2</span><div><strong>Résumé evidence</strong><p className="muted"><a href="/onboarding/resume">Upload or review your résumé</a></p></div></li><li><span className="step">3</span><div><strong>GitHub evidence</strong><p className="muted"><a href="/onboarding/github">Connect and select repositories</a></p></div></li></ol></section>
      <section className="card"><p className="eyebrow">RECENT MATCHES</p><h2>Your next opportunity starts with evidence.</h2><p className="muted">No matches yet. Job analysis will become available after evidence sources are ready.</p></section></div>
  </>
}
