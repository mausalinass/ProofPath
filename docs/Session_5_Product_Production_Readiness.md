# Session 5 - Product and production readiness

Date: 2026-10-30  
Environment: local validation and AWS staging configuration  
Data policy: synthetic data only

## Outcome

ProofPath now has production-oriented account verification and recovery, a versioned privacy notice and consent record, explicit data-deletion behavior, fail-closed real-data approval gates, stronger PDF parser separation, production database durability settings, and repeatable launch, rollback and restore procedures. Public registration and real-personal-data processing remain disabled until the owner completes the separate production approvals in the completion checklist.

## Account and privacy controls

- ASP.NET Identity email-confirmation and password-reset tokens use the persisted Data Protection key ring and expire after two hours.
- Registration, login, confirmation, forgot-password and reset-password endpoints remain CSRF protected and rate limited. Forgot-password responses do not reveal whether an account exists.
- Account email is delivered through Amazon SES using the ECS task role; no SMTP password or long-lived AWS access key is introduced.
- Production requires a verified sender, confirmed email, current privacy consent and an explicit public URL. Staging keeps account email disabled until an SES identity is configured.
- Public registration remains disabled by default. The production landing page does not advertise signup unless the build is explicitly configured to do so.
- The user-facing privacy notice is version `2026-10-30`; registration records acceptance time and version.
- Account deletion requires the current password, invalidates sessions, revokes GitHub installations, deletes relational user data and queues private résumé-object deletion. Current data and older résumé versions otherwise remain until account deletion.

## Provider and document decisions

- Synthetic staging approval does not authorize real résumé processing. API and Worker production startup both fail closed unless `OpenAI:RealDataProcessingApproved=true` is explicitly supplied in the protected production environment.
- `store=false`, provider-input redaction and the existing 30-day default-retention disclosure remain in force. Owner acceptance or an approved OpenAI retention control is still required before setting the production approval.
- The request-serving API no longer opens a PDF with PdfPig during upload validation. It performs bounded signature, size, active-content and malware checks; complex parsing and the 100-page limit execute in the separately deployed Worker task.
- Public registration remains closed, limiting exposure while final fuzzing and failure-recovery checks run during independent validation.

## Database durability and restore

- `ProductionDatabase=true` configures 35-day automated backups, deletion protection, Multi-AZ, snapshot tag copying and retention of automated backups. Staging defaults remain one day, single-AZ and deletion protection off to avoid an unapproved cost increase.
- Both replacement and deletion retain a final snapshot.
- `scripts/test-database-restore.ps1` created two isolated PostgreSQL 18 containers, inserted a synthetic record, produced a custom-format backup, restored it into the second instance, verified schema and data, and removed both temporary containers. The test passed on 2026-10-30.
- Final validation must additionally restore an actual production-like RDS snapshot into an isolated subnet before launch; that paid AWS exercise is intentionally not performed without cost approval.

## Validation

- Backend build completed with zero warnings and zero errors; all 93 tests passed.
- Frontend lint passed; all 13 tests passed; the production build passed.
- All three Playwright journeys passed against isolated PostgreSQL and private local storage.
- Dependency audits reported no known npm or NuGet vulnerability.
- CloudFormation templates and production Compose configuration validated.
- Database logical backup/restore passed with synthetic data.
- Secret-pattern review found no committed credential or private-key material.

## Remaining owner approvals

Session 5 is complete for synthetic final validation. These are deliberate production-launch gates, not inferred approvals:

1. Accept the OpenAI real-data retention arrangement or obtain approved Zero Data Retention / Modified Abuse Monitoring controls.
2. Configure and verify an SES sender and enable account email in the isolated production environment.
3. Approve production costs, support contacts, backup ownership and the final domain.
4. Explicitly decide whether public registration will ever be enabled; the release default is closed.

