# Final staging validation

Date: 2026-11-11. Environment: AWS staging, synthetic data only.

## Release and tests

- Frontend source `22bea1860d150c35696f8562924b7033849a1779`, previous release `c53d20a33a81d7b9bfd26d9648ee68984963a523`. The frontend package SHA-256 was `ef31207b753c94a68759e3fed7aa0d48fa0f7883e50f0f98d0dc50f2a5010db0` locally and in CloudShell.
- AWS accepted eleven conditional S3 writes, including two compatibility assets in the previous prefix. The deployment used the existing CloudFormation template and changed only the web origin path in CloudFront. The [source CI run](https://github.com/mausalinass/ProofPath/actions/runs/42695053756) passed all four jobs.
- Lint, production build, 13 unit tests, five isolated Playwright journeys and the synthetic PostgreSQL logical restore passed. The production npm dependency audit reported zero findings. The full npm audit reported 10 moderate development-dependency findings. The AWS RDS snapshot restore remains a separate launch gate.
- The promoted frontend passed 28 browser checks across seven routes and four viewport widths. The skip link moved focus to the workspace with a solid outline. Reduced motion and in-page navigation passed. Two open tabs retained the prior document and fetched its hashed assets successfully. Browser checks found zero unexpected errors and three anonymous session checks cancelled by navigation.
- Staging public registration remains disabled. The five isolated browser journeys cover account creation/deletion, GitHub evidence, résumé and Worker processing, opportunity matching and privacy. An authenticated run on the current staging release still needs a dedicated synthetic account and authorized test GitHub installation. Session 3 records a previous live run.

## Staging rollback drill

Initial promotion ran from 17:19:42 to 17:21:08 UTC. The rollback ran from 17:22:22 to 17:24:18 UTC (1 minute 56 seconds), including CloudFront deployment and cache invalidation. Fresh `/jobs` served the previous JavaScript, and the tab still running the corrected HTML fetched the new JavaScript and CSS with HTTP 200. Public HTML and both asset sets matched their SHA-256 manifests. Readiness stayed at HTTP 200 in the phase checks, and the protected profile returned HTTP 401 without a session.

The corrected version was reactivated from 17:25:43 to 17:27:40 UTC. Its HTML and both versions' assets passed the same hash and health checks again. The three change sets changed only CloudFront's web `OriginPath`. The previous release is available for rollback.

## Operational risk

On November 11, staging returned readiness HTTP 503 with PostgreSQL unhealthy. RDS was available and the Secrets Manager rotation timestamp (November 10, 16:10 UTC) followed the start of both API and Worker tasks (November 5). A rolling restart using the same definitions restored readiness to HTTP 200. The evidence is consistent with stale injected credentials. Rotation remains a recurring risk until an automatic refresh is deployed and exercised, or the owner accepts the documented manual recovery process and assigns coverage for the next rotation.

**High: alarm delivery failed.** CloudWatch recorded the 503, transitioned to `ALARM` at 17:17:44 UTC and back to `OK` at 17:18:44 UTC. Both SNS actions failed because CloudWatch Alarms could not access the SNS encryption key. The topic uses `alias/aws/sns`, and it has one confirmed subscription. Review a customer-managed KMS key and a topic policy scoped to this account's ProofPath alarms. After approval, verify successful SNS delivery with a controlled test. The observed AWS Budgets forecast was USD 101 against the USD 100 staging limit.

For the sample starting at 17:17 UTC, API, Worker and ClamAV returned zero matches for the specific exception and credential markers searched. CloudWatch separately reported the readiness failure above. These checks covered the sampled window and patterns.

The old staging workflow published directly to the S3 root and removed objects absent from the build. The new guard stops it before AWS mutations on a versioned staging site or an unexpected preflight error. Its sync no longer deletes objects. Use the [versioned staging procedure](Production_Runbook.md#versioned-staging-frontend-deployment-and-rollback) for staging.

GitHub's [Protect main ruleset](https://github.com/mausalinass/ProofPath/rules/27049708) requires a pull request, the four GitHub Actions checks and an up-to-date base. It blocks force pushes and branch deletion. The bypass list is empty and the review count is zero for the single-maintainer repository.

## Launch decision

The current decision is **hold**. Finish the authenticated staging run, address rotation and the SNS encryption issue, and record an isolated AWS snapshot restoration before the public release. The owner must select the final HTTPS domain, approve costs and real-data handling, designate support and recovery owners, and approve the production release. The production environment and live provider controls still need validation.
