# Session 1 staging recovery

- Date: 2026-10-26
- Environment: AWS staging, `us-east-2`
- Data used: no user or résumé data; health checks and infrastructure metadata only

## Outcome

Staging is healthy. The frontend and login page return HTTP 200, API liveness and readiness return HTTP 200, PostgreSQL reports healthy, the protected résumé endpoint returns HTTP 401 without authentication, and the API and Worker ECS services are stable at their desired count. No infrastructure or data was recreated or deleted.

## Root cause

The API task had started on 2026-10-17 and the Worker task on 2026-10-16. RDS rotated its managed master password on 2026-10-20. The task definitions correctly reference the managed Secrets Manager secret, but ECS injects secret values only when each task starts. The long-running tasks therefore retained the previous password and PostgreSQL readiness failed.

The diagnosis excluded other candidate causes:

- RDS was `available`, private and in the expected VPC.
- The database security group allowed port 5432 from the exact ECS task security group.
- API and Worker used the expected subnets, database endpoint, database name, username and managed-secret reference.
- A one-off task using API task definition revision 5, the same subnets and the same security group completed `dotnet ProofPath.Api.dll --migrate` with exit code 0. This proved current DNS, routing, TLS, security-group and secret-version connectivity from ECS without exposing the password.

## Recovery

The API and Worker services were forced to deploy their existing task definitions. The replacement tasks loaded the current secret version, reached steady state and restored PostgreSQL readiness. No CloudFormation stack, RDS instance, database, secret or stored data was replaced.

## Verification evidence

| Check | Result |
| --- | --- |
| ECS API service | stable, desired 1 / running 1 / pending 0 |
| ECS Worker service | stable, desired 1 / running 1 / pending 0 |
| `/` | HTTP 200 |
| `/login` | HTTP 200 |
| `/health/live` | HTTP 200, healthy |
| `/health/ready` | HTTP 200, PostgreSQL healthy |
| `/api/v1/resumes` without authentication | HTTP 401 |
| One-off migration task | API container exit code 0 |
| New API and Worker log streams | no sensitive-term matches |

The new Worker stream contained SQL command text with column names such as `ErrorCode`; these were false positives in a broad text search, not error-level events. The new API stream emitted one non-blocking startup message because the optional `libgssapi_krb5.so.2` Kerberos library is absent. Password authentication and PostgreSQL readiness are healthy, so this does not block Session 1; it remains a container-noise cleanup item.

## Tests

- 53 backend tests that do not require Docker passed.
- Frontend lint passed.
- 12 frontend unit tests passed.
- Frontend production build passed.
- The four Docker-backed production-readiness tests could not initialize because Docker Desktop was unavailable locally. Restoring Docker and rerunning the complete integration suite remains explicitly assigned to Session 2.

## Follow-up risk

A future managed RDS password rotation can produce the same stale ECS environment value. The operational recovery is documented in the production runbook. A later hardening session should automate a controlled API and Worker redeployment after rotation and add an actionable readiness alarm. Docker operation and the known .NET and npm dependency findings were resolved in Session 2 on 2026-10-26.
