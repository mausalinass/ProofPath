# Session 3 - Integrations and full synthetic journey

Date: 2026-10-26  
Environment: AWS staging, `https://d27i0fde08ymkz.cloudfront.net`  
Data policy: synthetic data only

## Outcome

The staging integration path was exercised end to end with a dedicated synthetic candidate and no database intervention. The session restored the GitHub App callback, browser-facing HTTPS scheme propagation, private résumé storage authorization and real-provider job extraction contract before repeating the journey successfully.

Public registration was enabled only on a temporary API task revision to create the synthetic candidate and was immediately returned to the protected revision. The immutable Session 3 deployment completed on API revision `proofpath-staging-api:12` and Worker revision `proofpath-staging-worker:7`; both services reported successful deployments with one running task. A follow-up registration request returned HTTP 403. No infrastructure or data was recreated or deleted.

## Findings and repairs

1. **Secure antiforgery cookie failed behind CloudFront.** The private ALB uses HTTP, so ASP.NET Core initially saw a non-HTTPS request. The API now honors `CloudFront-Forwarded-Proto`, and the `/api/*` and `/health*` behaviors forward the managed CloudFront viewer headers. Public CSRF acquisition now returns HTTP 200 with a secure cookie.
2. **GitHub App setup callback expected an OAuth code.** GitHub App installation callbacks supply `installation_id`, `setup_action` and the signed state. The callback now validates those fields, verifies the installation through the App API and keeps state single-use.
3. **Résumé storage failed before S3 upload.** `S3PrivateFileStore` checks the bucket public-access block before every operation, but the ECS task role lacked `s3:GetBucketPublicAccessBlock`. The role received only that bucket-scoped read permission, and `infra/foundation.yml` now preserves it. The bucket remained private and no object policy was weakened.
4. **Real job extraction rejected separately grounded phrases.** Machine validation required `originalWording` to be contained inside the selected quote even when both were exact phrases in the same source block. Validation now requires both fields independently to be present in the cited block. The synthetic job fixture also uses one explicit requirement per source block so its provenance is deterministic. Forged wording and citations still fail.

## Verified journey

- GitHub App callback: `https://d27i0fde08ymkz.cloudfront.net/api/v1/github/callback`.
- GitHub App installation: active, repository selection restricted, read-only Contents and Metadata, no suspension.
- Synthetic login and protected session: successful; unauthenticated `/api/v1/auth/me` returned HTTP 401.
- Repository synchronization and scan: `mausalinass/ProofPath` completed as `CompletedLimited`, fixed to commit `08f4f68f`, with traceable evidence including GitHub Actions, React, Docker, PostgreSQL, Entity Framework Core and ASP.NET Core.
- Malware control: a structurally valid synthetic PDF containing the standard EICAR test signature returned HTTP 422 and was not stored.
- Clean résumé: returned HTTP 202, analysis completed as `PartiallyCompleted` on the first attempt, and review exposed one fact plus eleven skills before confirmation.
- Job intelligence: real-provider extraction completed on the first attempt, 19 grounded requirements were reviewed and confirmed, and matching completed with full evaluation coverage. The resulting synthetic match contained 19 evaluated requirements and four gaps.
- Recommendations and tracking: four recommendations were produced, one moved to `InProgress`, the application moved to `Applied`, and one score-history entry was recorded.
- Failure, retry and cancellation: malformed machine extraction failed closed as non-retryable; finite backoff, ownership and manual-retry rules passed their automated tests; the live synthetic cancellation job reached `Cancelled`.
- OpenAI account: hard monthly limit remained enabled at USD 100 with spend at USD 0 during review; alerts were configured at 80% and 100%.
- Frontend, login, `/health/live`, `/health/ready` and CSRF endpoint returned HTTP 200; protected unauthenticated API returned HTTP 401.
- Public registration returned HTTP 403 after the temporary bootstrap task was removed.
- Recent API and Worker logs contained no failure entries after the successful journey and no matches for reviewed API-key or private-key signatures. Request parameters remained redacted.

The executable synthetic journey is `scripts/session3-synthetic-journey.ps1`. It creates only synthetic records, checks EICAR rejection, confirms résumé facts and job requirements, calculates a match, updates one recommendation and application tracking, creates and cancels a separate analysis job, and reports only sanitized states and counts.

## Recovery notes

If résumé upload returns HTTP 500 with `s3:GetBucketPublicAccessBlock` denied, compare the effective `PrivateResumeStorage` inline policy with `infra/foundation.yml`. Add only the bucket-scoped read action, wait for IAM propagation and retry with a synthetic file. Do not grant broad S3 access and do not disable the application's private-bucket check.

If a worker is replaced during analysis, an expired lease is recovered with the sanitized `WORKER_INTERRUPTED` code. Confirm whether the job eventually completes before treating the recovery event as a terminal failure. Manual retry is allowed only for terminal failures marked retryable; cancellation remains fenced by the lease token.

## Evidence and validation

- CI for the primary repair commit `15e4b70`: GitHub Actions run `40433462503`, successful.
- Immutable staging deployment for commit `43508ca`: GitHub Actions run `40433735258`, successful in 12 minutes 19 seconds.
- CloudFormation lint: `infra/foundation.yml`, `infra/production.yml` and `infra/frontend.yml`, successful.
- Backend: 91 tests passed after the real-provider contract repair; the focused job validation/provider subset passed 3 tests.
- Frontend: ESLint passed, 12 Vitest tests passed, production build passed.
- Final public checks: `/`, `/login`, `/health/live`, `/health/ready` and `/api/v1/auth/csrf` returned HTTP 200; unauthenticated `/api/v1/auth/me` returned HTTP 401; registration returned HTTP 403.
- Final synthetic journey: EICAR 422; clean upload 202; résumé `PartiallyCompleted`; job `Completed`; match `Complete`; recommendation `InProgress`; tracking `Applied`; cancellation `Cancelled`; 17 GitHub evidence items; `journey=completed`.

No secret value, résumé content, authentication cookie or provider response body is included in this report.
