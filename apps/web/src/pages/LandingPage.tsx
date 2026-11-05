import { CollageArt } from '../components/CollageArt'
import { ProgressPath } from '../components/ProgressPath'
import './LandingPage.css'

export function LandingPage() {
  const destination = import.meta.env.DEV || import.meta.env.VITE_REGISTRATION_ENABLED === 'true' ? '/signup' : '/login'
  return <div className="landing">
    <section className="landing-hero"><div className="landing-copy"><h1>Tu trabajo<br />habla por ti.</h1><p>Conecta GitHub y tu currículum. Descubre qué demuestra tu experiencia y cómo encaja con tu próxima oportunidad.</p><a className="button" href={destination}>Analizar mi perfil <span aria-hidden="true">↗</span></a><small>Para estudiantes y recién graduados de ingeniería.</small></div><div className="landing-collage"><CollageArt kind="evidence" /><CollageArt kind="github" /></div></section>
    <ProgressPath />
    <section className="landing-feature card yellow" id="how-it-works"><div><h2>Encuentra el encaje.<br />Entiende el porqué.</h2><p>Compara tu experiencia con una vacante. Identifica fortalezas, requisitos por cubrir y próximos pasos.</p><a className="button secondary" href="/jobs">Ver el proceso ↗</a></div><CollageArt kind="matching" /></section>
    <section className="landing-privacy"><CollageArt kind="privacy" /><div><h2>Tu perfil.<br />Tus decisiones.</h2><p>Elige los proyectos y el currículum que quieres analizar. Revisa la información que representa tu trabajo.</p><a href="/privacy">Leer el aviso de privacidad ↗</a></div></section>
    <section className="landing-close card green"><h2>Dale contexto a<br />lo que construiste.</h2><a className="button secondary" href={destination}>Comenzar ↗</a></section>
  </div>
}
