# Sprint 7 — Production Release

## Implemented locally

- Production containers for web, API and worker, all running as non-root users.
- Repeatable ECS/ALB/CloudWatch and S3/CloudFront CloudFormation application stacks.
- GitHub Actions CI with tests, dependency audits and container builds.
- OIDC-based production workflow with immutable ECR images, explicit security gates and controlled migrations.
- PostgreSQL readiness and process liveness endpoints.
- Structured privacy-safe request logging, trace identifiers, OpenTelemetry instrumentation and a CloudWatch 5xx alarm baseline.
- Public product landing page and stable demo seed/script.
- Security review, architecture diagram, operational runbook, rollback guidance and repository documentation.
- Persistent Data Protection keys, hardened Identity settings and production registration disabled by default.
- ALB origin verification: direct requests receive 403 unless they carry the CloudFront-only secret.

## Release gate

Staging is ready for an owner-reviewed launch: malware scanning, the private CloudFront VPC origin and the AWS foundation are implemented. The remaining actions are the explicit synthetic-data privacy approval, paid AWS foundation creation and the live validation described in [the current audit](Pre_Deployment_Audit.md).