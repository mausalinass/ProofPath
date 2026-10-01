# Production runbook

## Estado del release

No ejecute el workflow de producción hasta cerrar todos los bloqueos críticos de [la auditoría previa al despliegue](Pre_Deployment_Audit.md). El propio workflow falla de forma segura si faltan las revisiones de archivos, privacidad de IA, transporte o el secreto interno de origen.

## One-time AWS setup

Use us-east-2 consistently. Create a VPC with two application subnets and two private database subnets, an encrypted private RDS PostgreSQL instance, the private résumé bucket, ECR repositories, an application secret in Secrets Manager, and separate ECS execution/task roles. The task role receives access only to the résumé bucket. The execution role receives only ECR, CloudWatch Logs and the two named secrets.

Restrict the ALB security group to the AWS-managed CloudFront origin-facing prefix list. Add an HTTPS listener with an ACM certificate and change the CloudFront custom origin to https-only before setting the transport gate to true. The custom origin header is defense in depth and does not replace TLS or the security-group restriction.

Create a GitHub OIDC role restricted to repo:mausalinass/ProofPath:environment:production; never store AWS access keys in GitHub.

## Required GitHub production variables

AWS_DEPLOY_ROLE_ARN, AWS_REGION, AWS_VPC_ID, AWS_PUBLIC_SUBNETS, AWS_ALB_SECURITY_GROUPS, AWS_TASK_SECURITY_GROUPS, AWS_ECS_EXECUTION_ROLE_ARN, AWS_ECS_TASK_ROLE_ARN, PROOFPATH_APPLICATION_SECRET_ARN, PROOFPATH_DATABASE_SECRET_ARN, PROOFPATH_DATABASE_HOST, PROOFPATH_RESUME_BUCKET, PROOFPATH_MIGRATION_NETWORK, PROOFPATH_FILE_SECURITY_REVIEWED, PROOFPATH_OPENAI_PRIVACY_REVIEWED, and PROOFPATH_TRANSPORT_REVIEWED.

The three review variables must stay false until their controls exist and have evidence. They are release gates, not declarations of intent.

## Required GitHub production secret

PROOFPATH_ORIGIN_VERIFY_SECRET: a random value of at least 32 characters. Store it as an environment secret, never as a variable, repository file, command example, screenshot or chat message.

The AWS application secret is JSON with OpenAIApiKey, GitHubAppId, GitHubAppSlug, and GitHubPrivateKeyPem. Store only its ARN in GitHub variables. Never place the secret values in GitHub variables or repository files.

## Release

Run **Deploy production** only from the protected production environment after a successful CI run and a staging smoke test. It builds immutable containers, publishes them to ECR, updates CloudFormation, runs migrations as a one-off ECS task, publishes the web build, invalidates CloudFront, and verifies readiness.

After the first URL exists, update the GitHub App callback and verify the full authorization flow before accepting real users. Keep public registration disabled until email verification and password recovery are implemented.

## Rollback

Redeploy a previously successful commit. Container tags are immutable. Application changes must remain compatible with the previous database schema; destructive migrations require a separate reviewed maintenance plan. CloudFormation and RDS snapshots provide infrastructure and data recovery points.

## Demo data and video

After staging deployment, run scripts/seed-demo.ps1 with a dedicated demo email and a password supplied as SecureString. Record the stable flow in docs/Demo_Script.md. Delete or rotate the demo account after public presentations.