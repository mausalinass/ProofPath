import { useEffect, useState } from 'react'
import './App.css'

function App() {
  const [backendStatus, setBackendStatus] = useState('Backend status: unavailable')

  useEffect(() => {
    const checkHealth = async () => {
      try {
        const response = await fetch('http://localhost:5001/health')

        if (!response.ok) {
          throw new Error('Health check failed')
        }

        setBackendStatus('Backend status: healthy')
      } catch {
        setBackendStatus('Backend status: unavailable')
      }
    }

    checkHealth()
  }, [])

  return (
    <main style={{ padding: '2rem', fontFamily: 'sans-serif' }}>
      <h1>ProofPath</h1>
      <p>Career intelligence powered by evidence.</p>
      <p>{backendStatus}</p>
    </main>
  )
}

export default App
