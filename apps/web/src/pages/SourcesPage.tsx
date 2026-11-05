import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { github } from '../api/github'
import { resumes } from '../api/resumes'
import { CollageArt } from '../components/CollageArt'
import { ErrorNotice, Loading } from '../components/Feedback'
import { navigate } from '../navigation'

export function SourcesPage() {
  const client = useQueryClient()
  const connection = useQuery({ queryKey: ['github-status'], queryFn: github.status })
  const versions = useQuery({ queryKey: ['resumes'], queryFn: resumes.list })
  const [file, setFile] = useState<File | null>(null), [error, setError] = useState(''), [drag, setDrag] = useState(false)
  const connect = useMutation({ mutationFn: github.connect, onSuccess: data => window.location.assign(data.url) })
  const upload = useMutation({ mutationFn: () => resumes.upload(file!), onSuccess: result => { void client.invalidateQueries({ queryKey: ['resumes'] }); navigate('/onboarding/resume?resume=' + encodeURIComponent(result.id)) } })
  function pick(value?: File) {
    upload.reset(); setError(''); setFile(null)
    if (!value) return
    if (!/\.(pdf|docx)$/i.test(value.name) || value.size === 0 || value.size > 10 * 1024 * 1024) { setError('Selecciona un PDF o DOCX con contenido, de hasta 10 MB.'); return }
    setFile(value)
  }
  const active = versions.data?.find(v => v.active)
  return <><div className="page-heading"><p className="context-line">TU CUENTA → TUS FUENTES → TU ANÁLISIS</p><h1>Tu potencial<br />empieza aquí.</h1><p className="muted">Dos piezas de tu historia. Conecta tus proyectos y añade tu currículum para revisar lo que puedes demostrar.</p></div>
    <div className="sources-grid"><section className="card"><div className="source-heading"><div><h2>Tu GitHub.</h2><p>Lo que construiste también cuenta tu historia.</p></div><CollageArt kind="github" /></div>
      {connection.isPending ? <Loading /> : connection.isError ? <ErrorNotice message={connection.error.message} retry={() => void connection.refetch()} /> : connection.data.connected ? <><span className="badge active">Conectado · {connection.data.login}</span><p className="hint" style={{marginTop:20}}>Selecciona explícitamente los repositorios que quieres analizar.</p><a className="button" href="/onboarding/github">Elegir repositorios ↗</a></> : <><p>Conexión con la GitHub App de ProofPath. Permisos de solo lectura; tú eliges los repositorios.</p><button onClick={() => connect.mutate()} disabled={connect.isPending}>{connect.isPending ? 'Preparando GitHub…' : 'Conectar GitHub ↗'}</button></>}
      {connect.isError && <ErrorNotice message={connect.error.message} />}
    </section><section className="card yellow"><div className="source-heading"><div><h2>Tu currículum.</h2><p>Tu experiencia, con tus propias palabras.</p></div><CollageArt kind="evidence" /></div>
      <form onSubmit={e => { e.preventDefault(); if (file) upload.mutate() }}><div className="source-drop" data-drag={drag} onDragOver={e => { e.preventDefault(); if (!upload.isPending) setDrag(true) }} onDragLeave={() => setDrag(false)} onDrop={e => { e.preventDefault(); setDrag(false); if (!upload.isPending) pick(e.dataTransfer.files[0]) }}><p>Arrastra tu currículum aquí o selecciónalo.</p><label>Résumé file<input type="file" name="resume" accept=".pdf,.docx" disabled={upload.isPending} onChange={e => pick(e.target.files?.[0])} /></label><small>PDF o DOCX · Hasta 10 MB · Documento con texto</small></div>
        {file && <p role="status">{file.name} · {(file.size / 1024).toFixed(0)} KB</p>}{error && <ErrorNotice message={error} />}{upload.isError && <ErrorNotice message={upload.error.message} />}
        <button disabled={!file || upload.isPending}>{upload.isPending ? 'Subiendo de forma privada…' : 'Subir y revisar currículum ↗'}</button>
      </form>{versions.isPending ? <Loading /> : versions.isError ? <ErrorNotice message={versions.error.message} retry={() => void versions.refetch()} /> : <p className="hint" style={{marginTop:20}}>{active ? 'Versión activa: ' + active.fileName : 'Los datos se activan después de que revises y confirmes la extracción.'} <a href="/onboarding/resume">Ver versiones ↗</a></p>}
    </section></div><div className="sources-next"><div><strong>Tu perfil se construye con evidencia.</strong><p>Puedes revisar tu dossier mientras completas tus fuentes.</p></div><a className="button secondary" href="/home">Ver mi dossier ↗</a></div></>
}
