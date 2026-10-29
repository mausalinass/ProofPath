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

## Post-deployment validation

Use only synthetic documents. Update the GitHub App callback to the final CloudFront URL, then verify login, GitHub authorization, clean résumé upload, EICAR rejection, extraction/review, job ingestion, matching and tracker recommendations. Keep public registration disabled.

The staging GitHub App callback is:

```text
https://d27i0fde08ymkz.cloudfront.net/api/v1/github/callback
```

GitHub App setup redirects use `installation_id`, `setup_action` (`install` or `update`) and the signed ProofPath `state`. ProofPath verifies the installation and its read-only Contents/Metadata permissions with a short-lived App JWT; it does not require an OAuth `code`. A callback state is single-use and replay must fail.

Private résumé operations verify the bucket's public-access block before reading or writing objects. The ECS task role therefore needs `s3:GetBucketPublicAccessBlock` on the résumé bucket in addition to its object permissions and `s3:ListBucket`. If upload logs show that exact denied action, restore the bucket-scoped permission from `infra/foundation.yml`; do not disable the runtime privacy check or grant `s3:*`.

For a repeatable staging check, run `scripts/session3-synthetic-journey.ps1` only with a dedicated synthetic account and synthetic résumé. Its output is deliberately limited to HTTP statuses, sanitized job states and aggregate counts. Never use a real résumé or production credential in this script.

## Alarms, logs and owner notification

The application stack creates alarms for API 5xx responses, unhealthy ALB targets, missing API and Worker tasks, RDS CPU, RDS free storage and explicit readiness failures. Alarm and recovery actions publish to the foundation SNS topic. Container Insights provides ECS task-count metrics; API and Worker logs retain 30 days, ClamAV logs retain 14 days, and request logs correlate the response `X-Trace-Id` with the same sanitized trace identifier in CloudWatch.

The readiness middleware emits only status and trace ID when `/health/ready` is not HTTP 200. A metric filter converts that event into `ProofPath/ReadinessFailures`; it never records database errors, credentials, request bodies or résumé content. After deployment, confirm all `proofpath-staging-*` alarms have at least one action and the SNS subscription is `Confirmed`. Test delivery with the SNS console's publish function only with owner approval; do not force a production alarm by stopping services.

## IAM scope and justified exceptions

The GitHub OIDC trust is restricted to the exact ProofPath repository identity, the `staging` environment and the STS audience. The deploy role can update only `proofpath-*` stacks, push only the two ProofPath ECR repositories, pass only the reviewed CloudFormation/ECS roles, run only the staging migration task family, access the named résumé and web buckets, invalidate distributions in this account, and read the validation controls used by the workflow.

The CloudFormation service role uses explicit service action lists instead of service-wide permissions. Some resource wildcards remain justified: create/register APIs do not expose a resource ARN before creation, EC2 and load-balancer discovery APIs require `Resource: *`, and CloudFront control-plane operations include AWS-managed policies and global resources. The role is assumable only by CloudFormation and the deploy role can pass it only to named ProofPath stacks. Re-review these exceptions whenever a new stack resource is introduced.

## Résumé object and version retention

The private résumé bucket remains AES-256 encrypted, versioned and fully publicly blocked. Incomplete multipart uploads expire after one day, noncurrent object versions expire after 30 days, and expired delete markers are removed. Current résumé objects do not expire automatically because user-data deletion and current-object retention are a Session 5 product/privacy decision; deletion continues through the application's ownership-checked workflow.

## Production promotion

Production needs a separate environment, role trust, privacy acceptance for real résumés, user-facing privacy notice, email verification and password recovery. Do not promote staging approval automatically.
