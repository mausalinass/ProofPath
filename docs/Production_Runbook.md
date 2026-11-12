# Staging deployment runbook

## Purpose

The first AWS deployment is `staging` and accepts synthetic data only. It is separate from production and uses CloudFront as the only public entry point.

## One-time foundation

Deploy `infra/foundation.yml` in `us-east-2` as stack `proofpath-foundation` from an MFA-protected administrative session. The template creates:

- a VPC with two public task subnets and two isolated origin/database subnets;
- an encrypted private PostgreSQL RDS instance with a Secrets Manager password;
- an encrypted, versioned and publicly blocked résumé bucket;
- immutable ECR repositories;
- separate ECS execution and task roles;
- a CloudFormation service role;
- a GitHub OIDC role restricted to `mausalinass/ProofPath` and the `staging` environment;
- an optional USD 100 monthly budget with a forecast alert at 80%.

When `BudgetAlertEmail` is supplied, the foundation also creates the encrypted `proofpath-staging-operations` SNS topic, an email subscription, forecast and actual budget notifications, and exposes the topic to the application stack's alarms. The owner must confirm the SNS subscription email before alarms can deliver. Do not record the address in project documentation or logs.

The default CloudFront prefix-list ID is for `us-east-2`. Verify it in VPC > Managed prefix lists before deployment. If the GitHub OIDC provider already exists in the account, import or reuse it instead of creating a duplicate.

The foundation has paid resources. RDS, Fargate, the Application Load Balancer, public IPv4 addresses, logs and traffic can incur charges even at low usage. Remove the staging application and web stacks when testing ends; the résumé bucket and database snapshot are retained intentionally.

## Protected GitHub staging configuration

Create a protected GitHub environment named `staging`.

Variables:

- `AWS_REGION=us-east-2`
- `AWS_DEPLOY_ROLE_ARN`: output `GitHubDeployRoleArn` from `proofpath-foundation`
- `PROOFPATH_OPENAI_PRIVACY_REVIEWED=true` only after accepting `OpenAI_Privacy_Review.md` for synthetic data

Secret:

- `PROOFPATH_ORIGIN_VERIFY_SECRET`: random value of at least 32 characters

Populate the Secrets Manager secret output `ApplicationSecretArn` with JSON keys `OpenAIApiKey`, `GitHubAppId`, `GitHubAppSlug`, and `GitHubPrivateKeyPem`. The API rejects the bootstrap value `REPLACE`. Never put these values in GitHub variables, files, screenshots or chat.

## Deployment

Run the `Deploy staging` workflow only after CI succeeds. It builds immutable images, creates the private ECS origin, creates the CloudFront VPC origin, migrates the database, uploads the web build and checks:

- both ECS services become stable;
- `/health/ready` succeeds through CloudFront HTTPS;
- an unauthenticated private API request is rejected;
- every S3 public-access block remains enabled.

The internal ALB has no public route. Its security group accepts only the AWS-managed CloudFront origin-facing prefix list, and the listener also requires a secret header. CloudFront reaches it through a private VPC origin.

The frontend CloudFront behavior applies HSTS, CSP, frame denial, MIME sniffing protection, a strict referrer policy, Permissions Policy, COOP and CORP. The API keeps its stricter `default-src 'none'` policy. Deployment validation fails if the public frontend loses HSTS, CSP or Permissions Policy.

### CloudFront viewer scheme and antiforgery cookies

The API origin is HTTP inside the private VPC even though the browser-facing connection is HTTPS. The `/api/*` and `/health*` cache behaviors must therefore use the AWS managed `AllViewerAndCloudFrontHeaders-2022-06` origin request policy (`33f36d7e-f396-46d9-90e0-52428a34d9dc`). ASP.NET Core consumes its `CloudFront-Forwarded-Proto` header as the forwarded scheme before antiforgery executes.

If `/health/ready` is healthy but `/api/v1/auth/csrf` returns HTTP 500 with an antiforgery error about a secure cookie on a non-SSL request, confirm both behavior policy IDs and the API forwarded-header configuration. Do not weaken the cookie's `Secure` policy; restore the viewer protocol signal instead. Validate the repair through the public HTTPS URL and confirm the CSRF response includes a secure cookie.

## PostgreSQL readiness recovery after password rotation

RDS manages and rotates the master password in Secrets Manager. ECS resolves `Database__Password` only when a task starts; an already-running API or Worker does not receive a later secret version automatically. If `/health/live` succeeds but `/health/ready` reports PostgreSQL unhealthy after a managed rotation, compare the running task start times with the secret's `LastChangedDate` without retrieving the secret value.

Before changing anything, verify that RDS is `available`, the RDS and ECS tasks use the expected VPC and subnets, the database security group allows PostgreSQL from the ECS task security group, DNS is enabled, and both task definitions reference the managed RDS secret. Prove current connectivity with the existing migration task definition and network configuration; do not print or fetch the password:

```bash
aws ecs run-task \
  --cluster "$CLUSTER" \
  --task-definition "$API_TASK_DEFINITION" \
  --launch-type FARGATE \
  --network-configuration "$NETWORK_CONFIGURATION" \
  --overrides '{"containerOverrides":[{"name":"api","command":["dotnet","ProofPath.Api.dll","--migrate"]}]}'
```

If that task exits with code `0` and the active service tasks predate the secret change, replace the tasks without changing their task definitions or infrastructure:

```bash
aws ecs update-service --cluster "$CLUSTER" --service "$API_SERVICE" --force-new-deployment
aws ecs update-service --cluster "$CLUSTER" --service "$WORKER_SERVICE" --force-new-deployment
aws ecs wait services-stable --cluster "$CLUSTER" --services "$API_SERVICE" "$WORKER_SERVICE"
curl --fail "$PUBLIC_URL/health/ready"
```

Confirm the frontend and `/login` return HTTP 200, `/health/live` and `/health/ready` are healthy, and an unauthenticated protected endpoint returns HTTP 401. Review only the new API and Worker log streams for real warnings or exceptions and for sensitive-data indicators. Never output secret values. If the diagnostic task cannot connect, do not restart the services blindly; continue with RDS events, security groups, routes, DNS and task logs.

This recovery is operational rather than preventive. Until secret rotation is wired to an automatic ECS redeployment, repeat the procedure after a future RDS master-password rotation or perform a controlled service deployment immediately after rotation.

### Prepared staging rotation recovery

`infra/staging-rotation.yml` defines a separate staging-only stack with a disabled EventBridge rule by default. It matches the exact RDS secret ARN and the `AWSCURRENT` label, then requests a rolling refresh of only the two reviewed ECS services. The Lambda role can describe and update those services, write its own logs and acquire a short-lived lease in its own DynamoDB table. It cannot read the database secret or change the database. The lease serializes duplicate deliveries without reserved Lambda concurrency. The handler retries after an older rollout, skips services already deployed after the event, and refuses to restart a failed rollout.

1. Review current AWS costs, Lambda concurrency quota, account and region. Resolve the database secret ARN and service ARNs from stack metadata. Review a change set for only the new recovery stack with `RotationRuleState=DISABLED`.
2. Validate the exact event pattern against the native Secrets Manager event format. Confirm account, region, ARN and label exclusions. In the approved staging window, invoke the Lambda once with a clearly identified synthetic event matching the metadata. Record both ECS rollouts, health, API access and logs. Keep the manual recovery instructions available.
3. Enable the rule only after the staging drill passes. At the next rotation, verify actual native event delivery, both successful rollouts and readiness. The rollout takes time and can leave a short readiness gap. If the rule fails, stop automatic invocations by setting `RotationRuleState=DISABLED` and use the documented manual recovery.

## Versioned staging frontend deployment and rollback

Build the web client from the verified frontend commit and record the artifact hashes. Read the current `WebOriginPath` and verify the current HTML and asset hashes before staging the candidate. The `Deploy staging` workflow is for unversioned staging. Its preflight stops before deployment when the web origin is versioned, `releases/` contains objects, or verification fails unexpectedly.

1. Upload candidate files to `releases/<full-commit-sha>/` with conditional S3 puts (`If-None-Match: *`). Verify SHA-256, size and cache headers. Publish `index.html` after its assets.
2. Put the active release's content-hashed JS/CSS in the candidate prefix and the candidate's content-hashed JS/CSS in the active prefix. Use conditional puts and compare hashes if a key already exists. Test that an open tab can load its referenced assets from either origin. Keep both HTML documents intact.
3. In `proofpath-staging-web`, create a CloudFormation change set with the previous template and parameter values, changing only `WebOriginPath`. Inspect its property details. Execute only a single in-place `Distribution` modification at `/Properties/DistributionConfig/Origins/0/OriginPath`.
4. Wait for CloudFormation `UPDATE_COMPLETE` and CloudFront `Deployed`. Invalidate `/` and `/index.html` and wait for completion. Verify the HTML and asset hashes, a deep route, readiness HTTP 200 and protected API HTTP 401 before continuing.
5. For rollback, repeat the change-set procedure with the prior prefix. Verify that the previous HTML is served and that the newer asset URLs still return the correct bytes. Record timestamps and restore the corrected version only after its rollback criteria have been checked. Retain both sets of objects.

`main` uses the [Protect main ruleset](https://github.com/mausalinass/ProofPath/rules/27049708). Open a pull request against the current `main` and wait for `backend`, `frontend`, `e2e` and `containers-and-security` from GitHub Actions. The repository's single-maintainer flow requires zero separate reviewers. The rule has an empty bypass list and blocks branch deletion and force-pushes. If a rule itself breaks maintenance, the repository owner can edit that specific rule in Settings, record the reason and restore enforcement after recovery.

## Post-deployment validation

Use only synthetic documents. Update the GitHub App callback to the final CloudFront URL, then verify login, GitHub authorization, clean résumé upload, EICAR rejection, extraction/review, job ingestion, matching and tracker recommendations. Keep public registration disabled.

The staging GitHub App callback is:

```text
https://d27i0fde08ymkz.cloudfront.net/api/v1/github/callback
```

GitHub App setup redirects use `installation_id`, `setup_action` (`install` or `update`) and a single-use ProofPath `state`. The OAuth authorization with PKCE verifies that the signed-in candidate controls the personal installation and that its Contents and Metadata permissions are read-only. Configure both the Setup URL and the OAuth callback as described in [GitHub setup](GitHub_Setup.md). Connections created before ownership verification require reconnection. The migration retains older evidence in quarantine until authorization can be verified again.

For a failed repository analysis, record the job ID and search the Worker logs for that ID. Use the logged operation, status, GitHub request ID, rate-limit reset and error code to diagnose the failure. Keep tokens, provider response bodies, and repository content out of incident notes. The Worker retries transient failures up to three attempts and respects GitHub's wait time, including when an operator retries a job. Confirm with two synthetic accounts that each can analyze its own private repository and that an attempt to attach the other account's installation returns HTTP 403.

Private résumé operations verify the bucket's public-access block before reading or writing objects. The ECS task role therefore needs `s3:GetBucketPublicAccessBlock` on the résumé bucket in addition to its object permissions and `s3:ListBucket`. If upload logs show that exact denied action, restore the bucket-scoped permission from `infra/foundation.yml`; do not disable the runtime privacy check or grant `s3:*`.

For a repeatable staging check, run `scripts/session3-synthetic-journey.ps1` only with a dedicated synthetic account and synthetic résumé. Its output is deliberately limited to HTTP statuses, sanitized job states and aggregate counts. Never use a real résumé or production credential in this script.

## Account email, privacy and registration

Public registration is disabled by default. Production must keep `RegistrationEnabled=false` until the owner separately approves public access, real-personal-data processing, support and abuse handling. The frontend production build hides signup unless `VITE_REGISTRATION_ENABLED=true` is deliberately supplied.

Email confirmation and password recovery use short-lived ASP.NET Identity tokens protected by the database-persisted Data Protection key ring. Configure a verified Amazon SES identity in the production region, scope `ses:SendEmail` to that identity, and pass `AccountEmailEnabled=true`, `RequireConfirmedEmail=true` and the verified sender address. The API returns the same forgot-password response for known and unknown accounts and never logs reset or confirmation tokens.

The privacy notice is `/privacy`. New registrations must accept the exact `PrivacyVersion` supplied to the API. Account deletion requires the current password, revokes GitHub installations, removes relational data and queues private résumé-file deletion. Older résumé versions otherwise remain private until account deletion.

Staging's synthetic-data approval is not production approval. A stack named `production` cannot start its API or Worker unless the protected `RealDataProcessingApproved` value is explicitly true. Do not set it until the owner accepts the provider retention arrangement or obtains an approved retention control.

## Alarms, logs and owner notification

The application stack creates alarms for API 5xx responses, unhealthy ALB targets, missing API and Worker tasks, RDS CPU, RDS free storage and explicit readiness failures. Alarm and recovery actions publish to the foundation SNS topic. Container Insights provides ECS task-count metrics; API and Worker logs retain 30 days, ClamAV logs retain 14 days, and request logs correlate the response `X-Trace-Id` with the same sanitized trace identifier in CloudWatch.

The readiness middleware emits only status and trace ID when `/health/ready` is not HTTP 200. A metric filter converts that event into `ProofPath/ReadinessFailures`; it never records database errors, credentials, request bodies or résumé content. After deployment, confirm all `proofpath-staging-*` alarms have at least one action and the SNS subscription is `Confirmed`. Test delivery with the SNS console's publish function only with owner approval; do not force a production alarm by stopping services.

Inspect `describe-alarm-history --history-item-type Action` after a staging failure and recovery. On 2026-11-11 the legacy SNS actions failed because CloudWatch lacked access to the topic's AWS-managed encryption key. Staging uses the free `proofpath-staging-cloudwatch-email` configuration in AWS User Notifications for operational email. It filters the seven staging metric alarms in Ohio and sends `ALARM` and `OK` events immediately. The owner confirmed receiving the controlled readiness test message. Verify the configuration and the verified contact before relying on staging email. Production needs a separate test with its own alarms and verified recipient.

## IAM scope and justified exceptions

The GitHub OIDC trust is restricted to the exact ProofPath repository identity, the `staging` environment and the STS audience. The deploy role can update only `proofpath-*` stacks, push only the two ProofPath ECR repositories, pass only the reviewed CloudFormation/ECS roles, run only the staging migration task family, access the named résumé and web buckets, invalidate distributions in this account, and read the validation controls used by the workflow.

The CloudFormation service role uses explicit service action lists instead of service-wide permissions. Some resource wildcards remain justified: create/register APIs do not expose a resource ARN before creation, EC2 and load-balancer discovery APIs require `Resource: *`, and CloudFront control-plane operations include AWS-managed policies and global resources. The role is assumable only by CloudFormation and the deploy role can pass it only to named ProofPath stacks. Re-review these exceptions whenever a new stack resource is introduced.

## Résumé object and version retention

The private résumé bucket remains AES-256 encrypted, versioned and fully publicly blocked. Incomplete multipart uploads expire after one day, noncurrent object versions expire after 30 days, and expired delete markers are removed. Current résumé objects do not expire automatically because user-data deletion and current-object retention are a Session 5 product/privacy decision; deletion continues through the application's ownership-checked workflow.

Current résumé objects and relational records are retained while the account is active. Permanent account deletion removes the identity-owned relational graph and queues every current private object for retryable deletion. Database backups expire under the environment's backup schedule and are not used to resurrect deleted accounts into the live service.

## Production database backup and restore

Deploy the production foundation with `ProductionDatabase=true`. This selects Multi-AZ, 35-day automated backups, deletion protection, snapshot tag copying, retained automated backups and snapshot retention on replacement/deletion. Staging intentionally keeps its lower-cost defaults.

Before launch, restore an RDS snapshot into a new isolated database and validate migrations, row counts, ownership checks and application readiness without directing public traffic to it. Delete the temporary restored instance only after evidence is recorded and with owner approval for the destructive action. For a no-cost logical rehearsal, run `scripts/test-database-restore.ps1`; it uses only synthetic data and removes its disposable local containers.

## Production promotion

Production needs a separate environment, role trust, privacy acceptance for real résumés, user-facing privacy notice, email verification and password recovery. Do not promote staging approval automatically.

Use [Production_Launch_Checklist.md](Production_Launch_Checklist.md) for approval, launch and rollback. Never perform an in-place database rollback. Redeploy the previous immutable application first; if data rollback is required, restore the pre-deployment snapshot into isolation, validate it, and switch only through a reviewed change set.
