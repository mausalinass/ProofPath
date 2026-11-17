# Phase 3 security hardening

Staging validation on 2026-11-17 used synthetic accounts and private test repositories. Production remains disabled.

## Changes and verification

- PostgreSQL now has ownership policies covering 24 candidate-data tables. API connections set the authenticated identity on every checkout, including when a pooled connection is reused. Registration binds the newly created identity for the duration of the profile transaction.
- API and Worker use different database role groups. The Worker can claim and process jobs across candidates using policies limited to its tables and operations. The identity tables and Data Protection keys are unavailable to the Worker. Migrations use the schema owner.
- API JSON contracts reject unexpected fields. Authentication inputs are bounded, logout requires authentication, and an unverified-account response requires the correct password.
- The staging internal ALB drops invalid HTTP header fields. The CloudFormation template records that setting and the runtime role option.
- CI scans the full Git history with Gitleaks, runs CodeQL for C# and JavaScript/TypeScript, audits dependencies and scans the three application images. Trivy reports infrastructure findings. The initial scan found one critical and multiple high-severity package issues in the standalone Nginx web image. Building with updated Alpine packages and restoring the non-root runtime user cleared the reported issues. Staging serves the web build from S3 via CloudFront.

## Staging evidence

- ECS completed both deployments with one running task per service. `proofpath-staging-api:19` runs the image built from `e783d77` and logs into PostgreSQL as `proofpath_api`. `proofpath-staging-worker:12` uses `proofpath_worker`. The previous task definitions, API revision 18 and Worker revision 11, remain available for rollback. The temporary database probe tasks finished and their task definitions were deactivated.
- PostgreSQL reported RLS active for the API, zero candidate rows without authenticated context and no Worker access to Identity. The isolated integration test also verified that the API role cannot update another candidate or move ownership to that candidate, even with a pooled connection.
- The public frontend and `/health/ready` returned HTTP 200, with PostgreSQL healthy. HTTP redirects to HTTPS. The frontend sent HSTS and CSP, an unapproved origin received no CORS grant, and both test accounts received `Secure`, `HttpOnly` and `SameSite=Lax` session cookies. Authenticated API responses used `no-store`.
- Anonymous protected requests returned 401. Public registration returned 403 and repeated login attempts returned 429. An unexpected auth field returned 400 and a mutation without a CSRF token returned 400.
- Each synthetic account could read its own profile and repositories and complete a fresh GitHub analysis. Attempts to select the other account's repository returned 403. Requests for the other account's analysis returned 404.
- RDS remained private and encrypted. The résumé bucket uses AES-256 default encryption and blocks all public access. The new API and Worker log samples contained no indicators matching the credential or server-error patterns reviewed.
- [Pull request #26](https://github.com/mausalinass/ProofPath/pull/26) records the code and test changes. [CI run 44131479692](https://github.com/mausalinass/ProofPath/actions/runs/44131479692) passed backend, frontend, Playwright, container/security and both CodeQL jobs.

## Pending before production

- Carry the staging runtime roles and ALB setting forward when applying the next CloudFormation change set. The staging changes were applied directly to the reviewed services and load balancer.
- Move ECS tasks into private subnets after provisioning a route for their outbound GitHub, OpenAI and AWS service calls. The current staging VPC has no NAT gateway. The existing task security group admits API ingress through the internal load balancer.
- Encrypt traffic from CloudFront to the private ALB and set the viewer TLS policy with a suitable domain and certificate. Staging serves users over HTTPS with HSTS. The private origin uses HTTP and no issued ACM certificate was available in Ohio during validation.
- Select and verify abuse protection before public registration. Anonymous registration in staging is disabled. Extend and test credential rotation for the new runtime passwords. The existing EventBridge rule watches the RDS-managed owner secret. Preserve that owner credential for migrations and recovery.
- Triage infrastructure scanner findings for WAF, certificate/TLS and customer-managed keys for S3/SNS. S3 server-side encryption and RDS encryption are already configured. Review nine moderate npm dependency advisories and resume malformed-file handling with the Phase 5 upload work.

See the [Production Runbook](Production_Runbook.md) for role rollout and recovery.
