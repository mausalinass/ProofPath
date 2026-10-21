# ProofPath v1 completion checklist

Last audit: 2026-10-21

This document is the source of truth for finishing ProofPath v1. Historical sprint documents remain useful as evidence, but this checklist controls the remaining work.

## Definition of complete

ProofPath v1 is complete when every required item below is checked, production is reachable through its final HTTPS domain, the post-deployment checks pass, monitoring and rollback are operational, and no critical or high-severity finding remains open without a documented owner-approved exception.

Routine maintenance after launch (dependency updates, cost review, backup checks, monitoring and future features) is ongoing product operation and does not prevent declaring v1 complete.

## Starting point

- [x] Sprints 1-7 implemented.
- [x] Source code committed and pushed to GitHub.
- [x] AWS staging infrastructure created.
- [x] GitHub App created with read-only repository permissions.
- [x] OpenAI and AWS credentials stored outside Git.
- [x] Completion audit performed.
- [ ] Current staging readiness restored; PostgreSQL health is presently failing.

## 1. Complete the five remediation sessions

Each session includes its own relevant tests, documentation update, secret scan, commit and push. Routine subtasks should be completed in one continuous execution. Stop only for required login, cost approval or an irreversible external action.

### Session 1 - Stabilize staging

- [ ] Diagnose the unhealthy PostgreSQL readiness check.
- [ ] Verify RDS state, credentials, networking and ECS connectivity.
- [ ] Restore API and Worker health without recreating or losing existing data.
- [ ] Confirm `/health/ready` returns HTTP 200.
- [ ] Confirm the frontend, login and protected API behavior.
- [ ] Review logs for errors and accidental sensitive data.
- [ ] Record the cause, fix and recovery procedure.

Exit criterion: frontend, API, Worker and PostgreSQL are stable in staging.

### Session 2 - Dependencies, Docker and local quality baseline

- [ ] Update Vite and affected frontend dependencies.
- [ ] Update vulnerable .NET cryptography dependencies.
- [ ] Restore Docker Desktop operation.
- [ ] Run all backend tests, including Testcontainers tests.
- [ ] Run frontend lint, unit tests and production build.
- [ ] Run Playwright browser tests.
- [ ] Run npm and NuGet vulnerability audits.
- [ ] Resolve all critical and high-severity dependency findings.

Exit criterion: builds and tests pass, and no unresolved critical or high dependency vulnerability remains.

### Session 3 - Integrations and full synthetic journey

- [ ] Verify the deployed GitHub App callback URL.
- [ ] Verify login and GitHub authorization.
- [ ] Upload and process a clean synthetic resume.
- [ ] Verify EICAR/malware rejection.
- [ ] Verify resume extraction and user review.
- [ ] Import or create a job.
- [ ] Run matching and inspect its evidence.
- [ ] Verify recommendations and application tracking.
- [ ] Verify job failure, retry and cancellation behavior.
- [ ] Confirm OpenAI usage and cost limits.

Exit criterion: a synthetic user completes the full staging journey without database intervention.

### Session 4 - Security and DevOps hardening

- [ ] Add appropriate security headers to frontend responses from CloudFront.
- [ ] Reduce broad IAM permissions and document justified exceptions.
- [ ] Review GitHub OIDC and CloudFormation roles.
- [ ] Resolve the production ECS subnet and internal TLS design.
- [ ] Configure actionable alarms for API, ECS, RDS and readiness failures.
- [ ] Attach notification destinations to alarms.
- [ ] Confirm AWS budget and cost alerts.
- [ ] Validate logs, metrics and trace correlation.
- [ ] Reconfirm S3 encryption and public-access blocking.
- [ ] Define object and version retention rules.

Exit criterion: the infrastructure is protected, observable and able to notify the owner of failures or unexpected costs.

### Session 5 - Product and production readiness

- [ ] Implement email verification.
- [ ] Implement password recovery and secure reset.
- [ ] Decide and configure public registration.
- [ ] Publish a user-facing privacy notice and consent flow.
- [ ] Approve the OpenAI real-data retention arrangement or obtain an appropriate data-retention control.
- [ ] Define user-data and resume deletion/retention behavior.
- [ ] Review and harden untrusted PDF parsing isolation.
- [ ] Define production database backups, deletion protection and availability.
- [ ] Perform and document a database restore test.
- [ ] Update stale project, sprint, setup and runbook documentation.
- [ ] Prepare production launch and rollback checklists.

Exit criterion: ProofPath is functionally, legally and operationally ready for final validation.

## 2. Redesign the frontend

- [ ] Define the design direction, user journeys and responsive breakpoints.
- [ ] Produce and approve the main-page mockups.
- [ ] Implement the approved React component and style changes.
- [ ] Preserve authentication, upload, matching and tracking behavior.
- [ ] Validate mobile, tablet and desktop layouts.
- [ ] Validate keyboard navigation, focus states, contrast and accessible labels.
- [ ] Run frontend lint, tests, build and browser checks.

Exit criterion: the approved design is implemented accessibly without functional regressions.

## 3. Open the final Testing, Security and DevOps chat

- [ ] Start a dedicated chat in the Proof Path project.
- [ ] Provide this checklist and the current commit SHA as its starting context.
- [ ] Confirm the repository is clean and synchronized with GitHub.
- [ ] Treat that chat as independent final verification, not as evidence based only on earlier sprint reports.

## 4. Execute final validation in staging

- [ ] Run the complete backend, frontend and browser suites.
- [ ] Run dependency, secret and source security scans.
- [ ] Validate authentication, authorization, CSRF, cookies and rate limits.
- [ ] Validate IAM, Secrets Manager, S3, RDS, ECS and CloudFront controls.
- [ ] Run the complete synthetic product journey.
- [ ] Test accessibility and supported screen sizes.
- [ ] Test failure handling, alarms and operational recovery.
- [ ] Test database backup restoration.
- [ ] Test application rollback.
- [ ] Record evidence and severity for every finding.

Exit criterion: all required tests pass and no unaccepted critical or high-severity finding remains.

## 5. Resolve final validation findings

- [ ] Fix every blocking finding.
- [ ] Repeat affected tests after each fix.
- [ ] Repeat the complete regression suite after the final fix.
- [ ] Document any accepted lower-severity risk, owner and review date.
- [ ] Commit and push the verified release candidate.

Exit criterion: a specific immutable commit is approved as the production release candidate.

## 6. Approve production launch

- [ ] Review expected AWS, domain and third-party costs.
- [ ] Confirm the production privacy and real-data decision.
- [ ] Confirm support, incident and account-recovery contacts.
- [ ] Confirm backup, rollback and maintenance responsibilities.
- [ ] Record explicit owner approval for production deployment.

## 7. Configure the final domain

- [ ] Select or purchase the domain.
- [ ] Configure Route 53 or the chosen DNS provider.
- [ ] Issue and validate the required ACM certificate.
- [ ] Attach the domain and certificate to CloudFront.
- [ ] Configure DNS records.
- [ ] Update allowed origins and production configuration.
- [ ] Update GitHub App and other external callback URLs.
- [ ] Verify secure cookies and authentication on the final domain.

Exit criterion: the final HTTPS domain resolves correctly and all integrations recognize it.

## 8. Deploy production

- [ ] Create or update the isolated production infrastructure.
- [ ] Load production secrets without exposing their values.
- [ ] Deploy the approved immutable release candidate.
- [ ] Apply database migrations safely.
- [ ] Confirm ECS, RDS, CloudFront and background Worker health.
- [ ] Keep the rollback path ready throughout deployment.

## 9. Perform post-deployment validation

- [ ] Run production smoke tests on the final domain.
- [ ] Confirm login, protected routes and account recovery.
- [ ] Run the permitted production user journey.
- [ ] Confirm logs contain no secrets or resume contents.
- [ ] Confirm alarms and notifications operate.
- [ ] Confirm cost monitoring operates.
- [ ] Observe the deployment for an agreed stability period.
- [ ] Record the production version, date and final evidence.
- [ ] Mark ProofPath v1 complete.

## Session control

When a session finishes, update this file, commit it with the implementation and report exactly:

`Terminamos la Sesion N. Seguiremos con la Sesion N+1.`

After Session 5 report:

`Terminamos las cinco sesiones. ProofPath esta listo para el rediseno visual y la validacion final.`

