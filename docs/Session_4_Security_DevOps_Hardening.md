# Session 4 - Security and DevOps hardening

Date: 2026-10-29  
Environment: AWS staging, `us-east-2`  
Data policy: synthetic data only

## Outcome

Session 4 hardens the deployed CloudFront, IAM, monitoring, cost controls and private résumé storage without recreating or deleting infrastructure or data. Deployment evidence is recorded after the reviewed CloudFormation changes complete.

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
- Live AWS resources, public headers, alarm actions, SNS subscription, budget and S3 controls: pending deployment record.

No secret value, notification address, authentication cookie, résumé content or provider response body is included in this report.
