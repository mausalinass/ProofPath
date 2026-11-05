import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { Session } from '../api/client'
import { github } from '../api/github'
import { resumes } from '../api/resumes'
import { ErrorNotice, Loading } from '../components/Feedback'
import { CollageArt } from '../components/CollageArt'
import { navigate } from '../navigation'

export function SettingsPage({ session }: { session: Session }) {
  const client = useQueryClient()
  const [confirm, setConfirm] = useState(false), [revoke, setRevoke] = useState(false)
  const [password, setPassword] = useState('')
  const connection = useQuery({ queryKey: ['github-status'], queryFn: github.status })
  const versions = useQuery({ queryKey: ['resumes'], queryFn: resumes.list })
  const disconnect = useMutation({ mutationFn: github.disconnect, onSuccess: () => { setRevoke(false); client.removeQueries({ queryKey: ['github-repositories'] }); void client.invalidateQueries({ queryKey: ['github-status'] }); void client.invalidateQueries({ queryKey: ['github-evidence'] }); void client.invalidateQueries({ queryKey: ['job-match'] }) } })
  const deletion = useMutation({ mutationFn: () => api.deleteAccount(password), onSuccess: () => { client.clear(); navigate('/login') } })
  return <><div className="privacy-title"><div><p className="context-line">PRIVACY & DATA CONTROL</p><h1>Tu perfil.<br />Tus decisiones.</h1><p className="muted">Revisa tus fuentes, entiende qué se conserva y decide cuándo cerrar el acceso.</p></div><CollageArt kind="privacy" /></div>
    <div className="privacy-grid"><section className="card"><h2>Tu conexión GitHub.</h2><p className="hint">{session.email}</p>
      {connection.isPending ? <Loading /> : connection.isError ? <ErrorNotice message={connection.error.message} retry={() => void connection.refetch()} /> : <><span className={'badge ' + (connection.data.connected ? 'active' : 'stale')}>{connection.data.connected ? 'Conectado · ' + connection.data.login : 'Sin conexión activa'}</span><article className="next-action"><h3>Solo lectura.</h3><p>Metadata y contenido de los repositorios que autorizas y seleccionas. ProofPath no escribe código ni crea commits en tu cuenta.</p></article><article className="next-action"><h3>Revocar no es borrar.</h3><p>Desconectar detiene nuevas lecturas y análisis. La evidencia histórica se conserva marcada como obsoleta hasta que elimines tu cuenta.</p></article><div className="actions"><button className="secondary" disabled={!connection.data.connected || disconnect.isPending} onClick={() => setRevoke(true)}>Revocar acceso</button><a href="/onboarding/github">Ver repositorios ↗</a></div></>}
      {revoke && <div className="privacy-confirm"><h3>¿Revocar el acceso?</h3><p>Se detendrá la conexión real con GitHub. Tus repositorios no se modificarán.</p><div className="actions"><button onClick={() => disconnect.mutate()} disabled={disconnect.isPending}>{disconnect.isPending ? 'Revocando…' : 'Confirmar revocación'}</button><button className="secondary" disabled={disconnect.isPending} onClick={() => { setRevoke(false); disconnect.reset() }}>Conservar conexión</button></div></div>}
      {disconnect.isError && <ErrorNotice message={disconnect.error.message} />}{disconnect.isSuccess && <p className="notice success" role="status">Acceso revocado. La evidencia anterior queda marcada como obsoleta.</p>}
    </section><section className="card yellow"><h2>La historia de tus datos.</h2><p>Versiones reales de tu currículum. Las revisiones del código y las comparaciones conservan sus propios snapshots.</p>
      {versions.isPending ? <Loading /> : versions.isError ? <ErrorNotice message={versions.error.message} retry={() => void versions.refetch()} /> : versions.data.length ? <ol className="version-history">{versions.data.map(v => <li key={v.id}><strong>v{v.version} · {v.active ? 'Activa' : v.status}</strong><p>{v.fileName}<br />{new Date(v.createdAt).toLocaleString()}</p><a href={'/onboarding/resume?resume=' + v.id}>Revisar versión ↗</a></li>)}</ol> : <p>No hay versiones guardadas. <a href="/sources">Añade tu primera fuente.</a></p>}
      <div className="actions"><a href="/jobs">Historial de comparaciones ↗</a><a href="/privacy">Retención y privacidad ↗</a></div></section></div>
    <section className="card danger-zone"><h2>Borrar mi cuenta e información.</h2><p>Elimina permanentemente tu cuenta, perfil, currículums, evidencias, comparaciones y seguimiento. La limpieza de los archivos privados se procesa automáticamente; las copias de seguridad expiran según su política de retención. No se puede deshacer.</p>
      {!confirm ? <button className="danger" onClick={() => setConfirm(true)}>Delete my account</button> : <form onSubmit={e => { e.preventDefault(); deletion.mutate() }}>
        <p role="alert">Confirma la eliminación permanente con tu contraseña actual.</p><label>Current password<input type="password" name="currentPassword" required autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} /></label>
        {deletion.isError && <ErrorNotice message={deletion.error.message} />}
        <div className="actions"><button className="danger" disabled={deletion.isPending}>{deletion.isPending ? 'Deleting…' : 'Permanently delete account'}</button><button type="button" className="secondary" disabled={deletion.isPending} onClick={() => { setConfirm(false); setPassword(''); deletion.reset() }}>Cancel</button></div>
      </form>}
    </section>
  </>
}
