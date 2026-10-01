import './LandingPage.css'

const features = [
  ['Evidence, organized', 'Turn résumés and selected GitHub work into a private, reviewable evidence profile.'],
  ['Matches you can explain', 'See why a role fits, what is missing, and which evidence supports each requirement.'],
  ['A plan you can act on', 'Track applications and follow focused recommendations instead of chasing a black-box score.'],
]

export function LandingPage() {
  return <div className="landing">
    <header className="landing-nav"><a className="brand" href="/">ProofPath<span>Evidence for your next step.</span></a><nav aria-label="Public navigation"><a href="#how-it-works">How it works</a><a href="/login">Sign in</a><a className="button" href="/signup">Create workspace</a></nav></header>
    <main>
      <section className="landing-hero"><div><p className="eyebrow">PRIVATE CAREER INTELLIGENCE</p><h1>Know what your experience proves.</h1><p className="landing-lead">ProofPath connects your real work to real job requirements, with transparent evidence and practical next steps.</p><div className="actions"><a className="button" href="/signup">Build your evidence profile</a><a className="button secondary" href="#how-it-works">See how it works</a></div><p className="landing-note">Your résumé and repository analysis remain private. You choose the sources.</p></div><aside className="match-preview" aria-label="Example match explanation"><span className="badge">EXPLAINABLE MATCH</span><strong>Backend Engineer · 82%</strong><div className="meter"><span /></div><p>Strong evidence for APIs, PostgreSQL, Docker, and delivery ownership.</p><ul><li>4 requirements supported</li><li>2 growth areas identified</li><li>Evidence linked to every signal</li></ul></aside></section>
      <section className="landing-section" id="how-it-works"><p className="eyebrow">HOW IT WORKS</p><h2>From scattered experience to a clear story</h2><div className="feature-grid">{features.map(([title, detail], index) => <article key={title}><span>{index + 1}</span><h3>{title}</h3><p>{detail}</p></article>)}</div></section>
      <section className="privacy-band"><div><p className="eyebrow">BUILT FOR TRUST</p><h2>Evidence without exposure</h2></div><p>Private storage, read-only GitHub access, account deletion, and traceable recommendations are part of the product design.</p><a className="button" href="/signup">Start privately</a></section>
    </main>
    <footer className="landing-footer"><span>ProofPath</span><span>Evidence over claims.</span></footer>
  </div>
}
