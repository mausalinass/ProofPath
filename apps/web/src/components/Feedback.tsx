export function ErrorNotice({ message, retry }: { message: string; retry?: () => void }) {
  return <div className="notice error" role="alert"><p>{message}</p>{retry && <button className="secondary" onClick={retry}>Try again</button>}</div>
}
export function Loading({ message = 'Loading your workspace…' }: { message?: string }) {
  return <p className="notice" role="status">{message}</p>
}
