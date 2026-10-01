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

## Post-deployment validation

Use only synthetic documents. Update the GitHub App callback to the final CloudFront URL, then verify login, GitHub authorization, clean résumé upload, EICAR rejection, extraction/review, job ingestion, matching and tracker recommendations. Keep public registration disabled.

## Production promotion

Production needs a separate environment, role trust, privacy acceptance for real résumés, user-facing privacy notice, email verification and password recovery. Do not promote staging approval automatically.
