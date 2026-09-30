import { defineConfig } from '@playwright/test'
if (!process.env.PROOFPATH_E2E_CONNECTION) throw new Error('Set an isolated PostgreSQL test connection before running E2E.')
if (!process.env.PROOFPATH_E2E_STORAGE) throw new Error('Set isolated private storage before running E2E.')
export default defineConfig({ testDir: './e2e', workers: 1, timeout: 60000, expect: { timeout: 15000 },
  use: { baseURL: 'http://localhost:5173', trace: 'retain-on-failure' },
  webServer: [
    { command: 'dotnet run --project ../backend/tests/ProofPath.E2EHost --no-launch-profile -- --urls http://localhost:5002', url: 'http://localhost:5002/health', reuseExistingServer: false },
    { command: 'dotnet run --project ../backend/src/ProofPath.Api --no-launch-profile -- --urls http://localhost:5001', url: 'http://localhost:5001/health', reuseExistingServer: false, env: { ASPNETCORE_ENVIRONMENT: 'Testing', ConnectionStrings__DefaultConnection: process.env.PROOFPATH_E2E_CONNECTION, Security__AuthRequestsPerMinute: '10000', Storage__PrivateRoot: process.env.PROOFPATH_E2E_STORAGE, OpenAI__ApiKey: '' } },
    { command: 'npm run dev -- --host localhost', url: 'http://localhost:5173', reuseExistingServer: false },
  ],
})
