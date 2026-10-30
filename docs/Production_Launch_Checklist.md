# Production launch and rollback checklist

## Approval gate

- [ ] Record owner approval for expected AWS, domain, SES and OpenAI costs.
- [ ] Record the real-data privacy/retention decision; set the protected production approval only after acceptance.
- [ ] Confirm support, incident, security and account-recovery contacts.
- [ ] Keep public registration disabled unless a separate reviewed decision enables it.
- [ ] Verify the SES sender identity and account-email delivery in the production region.

## Infrastructure and data

- [ ] Use an isolated production account/environment, private workload subnets, redundant NAT and HTTPS to the internal origin.
- [ ] Deploy the foundation with `ProductionDatabase=true`; verify Multi-AZ, 35-day backups, encryption and deletion protection.
- [ ] Restore a production-like RDS snapshot into an isolated test database and record evidence before launch.
- [ ] Verify private S3 encryption, public-access blocking, versioning, lifecycle rules and deletion queue operation.
- [ ] Load secrets through Secrets Manager without printing values.

## Release and launch

- [ ] Select the immutable release-candidate commit and image digests.
- [ ] Run complete backend, frontend, browser, dependency and secret scans.
- [ ] Configure the final domain, ACM certificate, origins, callbacks and secure cookies.
- [ ] Apply migrations with the migration task; confirm its successful exit before updating services.
- [ ] Verify API, Worker, RDS, CloudFront, alarms, SES and cost notifications.
- [ ] Run smoke tests with the permitted synthetic account and observe the agreed stability period.

## Rollback trigger and procedure

- [ ] Declare rollback for failed readiness, migration incompatibility, authentication/recovery failure, sensitive logging, elevated 5xx, data corruption or a critical/high security finding.
- [ ] Stop new deployment progression; do not delete stacks, the database, snapshots or résumé storage.
- [ ] Redeploy the last known-good immutable API and Worker task definitions and web artifact.
- [ ] If a migration is not backward compatible, restore the pre-deployment snapshot into an isolated database, validate it, then switch only through a reviewed change set.
- [ ] Re-run readiness, login, protected API, account recovery and synthetic journey checks.
- [ ] Record the incident, affected version, recovery evidence and follow-up owner.
