# Production architecture

```mermaid
flowchart LR
  U[Candidate browser] --> CF[CloudFront]
  CF --> WEB[Private S3 web bucket]
  U --> ALB[Application Load Balancer]
  ALB --> API[ECS Fargate API]
  API --> DB[(Private RDS PostgreSQL)]
  API --> RES[Private S3 résumé bucket]
  API --> SEC[Secrets Manager]
  API --> GH[GitHub App API]
  API --> OAI[OpenAI Responses API]
  WORK[ECS Fargate worker] --> DB
  WORK --> RES
  WORK --> GH
  WORK --> OAI
  API --> CW[CloudWatch logs, metrics and alarms]
  WORK --> CW
  GHA[GitHub Actions via OIDC] --> ECR[ECR immutable images]
  ECR --> API
  ECR --> WORK
```

The browser uses secure cookies and CSRF tokens. Résumés stay in a private bucket. The worker consumes durable PostgreSQL jobs, so API restarts do not lose analysis work. Production secrets are injected at runtime and never committed. OpenTelemetry instrumentation can export traces and metrics through an approved OTLP collector; CloudWatch container insights, structured logs, health checks, and a 5xx alarm form the baseline.

The supplied application stack accepts existing network, database, bucket, secret, and narrowly scoped IAM role identifiers. This separation prevents a repository workflow from creating its own administrator role.
