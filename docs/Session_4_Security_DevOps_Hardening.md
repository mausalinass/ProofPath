# Session 4 - Security and DevOps hardening

Date: 2026-10-29  
Environment: AWS staging, `us-east-2`  
Data policy: synthetic data only

## Outcome

Session 4 hardens the deployed CloudFront, IAM, monitoring, cost controls and private résumé storage without recreating or deleting infrastructure or data. The reviewed foundation update and the staging application deployment completed without resource replacement or data deletion.

## Implemented controls

- CloudFront frontend security headers: HSTS with preload, CSP, `X-Content-Type-Options`, frame denial, strict referrer policy, Permissions Policy, COOP and CORP.
- GitHub OIDC deploy scope reduced to named ProofPath stacks, repositories, roles, task family, staging services and named S3 buckets. CloudFormation service permissions use explicit action lists; unavoidable create/discovery and CloudFront exceptions are documented.
- Seven actionable alarms: API 5xx, unhealthy target, API task count, Worker task count, database CPU, database free storage and readiness failures.
- Readiness failures become a custom CloudWatch metric without logging dependency details or sensitive values.
- Alarm and recovery actions publish to the encrypted operational SNS topic when the owner notification email is configured.
- Monthly cost controls include forecast 80%, actual 80% and actual 100% notifications against the USD 100 staging budget.
- API responses retain trace correlation through `X-Trace-Id` and sanitized CloudWatch logs; API/Worker retention is 30 days and ClamAV retention is 14 days.
- The résumé bucket remains AES-256 encrypted, versioned and fully publicly blocked. Noncurrent versions expire after 30 days, incomplete multipart uploads after one day, and expired delete markers are removed.
- The production subnet and TLS design is explicit: private ECS workload subnets, redundant NAT egress, service endpoints and ACM-backed HTTPS to the internal ALB. Staging's lower-cost public-egress task design is not approved for production.
- GitHub Actions runtime majors were updated to remove the Node.js 20 deprecation path.

## Validation

- Backend: 91 tests passed; NuGet reported no vulnerable direct or transitive packages.
- Frontend: lint, 12 tests and production build passed; `npm audit --omit=dev` reported zero vulnerabilities.
- CloudFormation lint passed for all three templates and the production Compose configuration validated with a synthetic password.
- GitHub Actions CI run `40961108639` passed all four jobs: backend, frontend, E2E, and containers/security.
- Foundation stack `proofpath-foundation` reached `UPDATE_COMPLETE` after the reviewed change set `proofpath-session4-hardening`; its seven changes contained no replacement or deletion.
- Staging deployment run `40962777795` deployed immutable API/Worker images, both CloudFormation application passes and the CloudFront frontend. CI run `40962777623` passed all four jobs for the deployment commit.
- ECS reported successful deployments for API revision 14 and Worker revision 8, each with one desired/running task. PostgreSQL-backed `/health/ready` returned HTTP 200 after deployment.
- Public `/` and `/login` returned HTTP 200 with two-year HSTS preload, restrictive CSP, `nosniff` and Permissions Policy headers. `/api/v1/resumes` returned HTTP 401 without authentication; the CSRF endpoint returned HTTP 200 with `X-Trace-Id`.
- A generated `X-Trace-Id` was found in the corresponding sanitized API log event. No recent API/Worker error matches or private-key material were found during the log review.
- The deployment gate confirmed seven CloudWatch alarms with alarm actions, and confirmed the résumé bucket's public-access block, versioning, AES-256 encryption and three lifecycle controls.
- The monthly USD 100 budget is healthy and has forecast 80%, actual 80% and actual 100% alerts. The owner confirmed one direct AWS Budgets recipient and the operational SNS email subscription. An additional redundant direct recipient remains pending, but both required delivery paths are operational.

## Recovery and rollback

- Application rollback: redeploy the last known-good immutable commit through `Deploy staging`; CloudFormation retains prior task definitions and ECS replaces tasks without database changes.
- Infrastructure rollback: create and review a CloudFormation change set from the previous template revision before execution. Do not delete the stacks, résumé bucket, database or Secrets Manager entries.
- Notification recovery: if the AWS verification page requires a console login, an IAM administrator or the root user can reset the existing IAM user's console password. Confirm both the AWS Budgets recipient and SNS subscription links, then recheck that each status is verified/confirmed.

## Closure

The technical hardening, deployment and notification exit criterion are complete. Direct budget delivery and SNS operational delivery each have a confirmed recipient. A redundant pending direct recipient can be verified later without blocking subsequent work.

No secret value, notification address, authentication cookie, résumé content or provider response body is included in this report.
