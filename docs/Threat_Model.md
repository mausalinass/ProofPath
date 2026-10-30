# Security review

## Protected assets

- Account sessions and identities
- Résumés and extracted evidence
- GitHub installation access
- OpenAI, GitHub, database and AWS credentials
- Job descriptions, matches, recommendations and application notes

## Trust boundaries and controls

| Boundary | Main risks | Controls |
| --- | --- | --- |
| Browser to API | Session theft, CSRF, brute force, account takeover | Secure HTTP-only host cookies, explicit origins, antiforgery token, login rate limit, lockout, confirmed email, short-lived reset tokens, HSTS and security headers |
| CloudFront to ALB | Direct-origin bypass, header spoofing, interception | Internal ALB, CloudFront origin-facing prefix-list restriction, default 403 and secret custom origin header; production requires the documented ACM-backed HTTPS origin design |
| API/worker to storage | Public or cross-user file access, malicious upload | Private encrypted S3, blocked public access, per-user ownership, fail-closed malware scan and scoped task role; complex PDF parsing runs only in the separate Worker task |
| GitHub | Excess repository access | GitHub App, selected repositories, read-only Contents and Metadata, short-lived installation tokens |
| AI provider | Prompt injection and sensitive text | Structured extraction boundary, deterministic validation, redaction, `store=false`, no credentials in prompts/logs, and fail-closed production real-data approval |
| Database | Credential disclosure, public access | Private encrypted RDS, Secrets Manager, security-group-only access and persistent encrypted session-key material |
| Delivery | Long-lived cloud keys, image replacement | GitHub OIDC, immutable ECR tags, dependency audits, container builds and protected production environment |

## Logging policy

Logs may contain job IDs, event names, status codes, durations and trace IDs. They must not contain résumé text, job descriptions, passwords, cookies, tokens, API keys, private keys or connection strings. Retention defaults to 30 days.

## Release checks

1. Close every critical item in Pre_Deployment_Audit.md.
2. Confirm S3 public access block, encryption, versioning, noncurrent-version retention and malware quarantine.
3. Confirm GitHub App permissions remain read-only and update the public callback.
4. Confirm deploy/task/execution roles are limited to named ProofPath resources.
5. Rotate any credential copied into chat, screenshots or local shell history.
6. Run secret-history and dependency audits, all tests and the Playwright flow.
7. Verify /health/live and /health/ready; inspect alarms and cost budget.
