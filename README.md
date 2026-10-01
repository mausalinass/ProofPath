# ProofPath

ProofPath is a private, evidence-based career intelligence workspace. It connects a candidate's résumé and selected GitHub work to job requirements, explains strengths and gaps, recommends practical next steps, and tracks applications over time.

## Product flow

1. Create a private candidate profile.
2. Upload and review résumé evidence.
3. Connect a read-only GitHub App and select repositories.
4. Add a job description and confirm its extracted requirements.
5. Review an explainable match, evidence, confidence, gaps and recommendations.
6. Track the application and rescan when evidence changes.

## Architecture

- React and TypeScript frontend
- ASP.NET Core API and durable background worker
- PostgreSQL persistence and private S3 résumé storage
- GitHub App with read-only repository access
- OpenAI Responses integration behind a deterministic validation boundary
- AWS ECS Fargate, ALB, RDS, S3, CloudFront, Secrets Manager, ECR and CloudWatch production plan

See [the architecture diagram](docs/Architecture.md) and [security review](docs/Threat_Model.md).

## Local development

Requirements: .NET 10 SDK, Node.js 22, PostgreSQL 18 and Docker for the full integration suite.

```powershell
dotnet restore apps/backend/ProofPath.slnx
dotnet test apps/backend/ProofPath.slnx
cd apps/web
npm ci
npm test
npm run dev
```

Configuration examples live in `.env.example` and the application development settings. Secrets belong in .NET user secrets or environment variables and must never be committed.

## Validation

CI builds and tests the backend and frontend, runs the browser flow against PostgreSQL, audits NuGet/npm dependencies, and builds all production containers. The API exposes `/health/live` for the process and `/health/ready` for PostgreSQL readiness.

## Production

Production uses GitHub OIDC rather than long-lived AWS keys. Review [the pre-deployment audit](docs/Pre_Deployment_Audit.md) and [the production runbook](docs/Production_Runbook.md) before provisioning resources or running the protected deployment workflow. Infrastructure files are in `infra/`; the repository does not create an administrator role.

## Delivery record

- [Execution status](docs/Execution_Status.md)
- [Sprint 2](docs/Sprint_2_Status.md)
- [Sprint 3](docs/Sprint_3_Status.md)
- [Sprint 4](docs/Sprint_4_Status.md)
- [Sprint 5](docs/Sprint_5_Status.md)
- [Sprint 6](docs/Sprint_6_Status.md)
- [Sprint 7](docs/Sprint_7_Status.md)
