import { useSyncExternalStore } from 'react'
const subscribe = (listener: () => void) => { window.addEventListener('popstate', listener); return () => window.removeEventListener('popstate', listener) }
export function navigate(path: string) { window.history.pushState({}, '', path); window.dispatchEvent(new PopStateEvent('popstate')); window.scrollTo(0, 0) }
export function usePath() { return useSyncExternalStore(subscribe, () => window.location.pathname + window.location.search) }
