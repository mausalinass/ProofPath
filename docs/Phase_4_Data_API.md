# Phase 4 data API

All routes use the authenticated session. Mutations require a valid CSRF token. The API derives the candidate from the cookie and PostgreSQL also enforces the Phase 3 row policies for the underlying tables.

| Route | Behavior |
| --- | --- |
| `GET /api/v1/profile` | Read the current candidate's professional profile. |
| `PUT /api/v1/profile` | Replace the editable fields on the current profile. Limits are 100 characters for each name, 200 for headline or work authorization, 150 for location and 2,000 for education summary. |
| `GET /api/v1/github/repositories` | List repositories from the current candidate's verified installation with their selection and scan state. |
| `PUT /api/v1/github/repositories/selection/{repositoryId}` | Add an authorized GitHub repository ID to the current selection. Up to five repositories can be selected. |
| `DELETE /api/v1/github/repositories/selection/{repositoryId}` | Remove that repository from the selection and deactivate its active evidence. The original analyses and historical results remain available. |
| `GET /api/v1/profile/history/evaluations` | List the current candidate's persisted résumé, job description and repository evaluations, including state, timestamps and retry information. |
| `GET /api/v1/profile/history/matches` | List the current candidate's stored matches across jobs with IDs for the existing job and match detail routes. |

Single-repository selection is idempotent. A foreign or unknown repository returns 404. The existing bulk selection contract remains available. Selection changes are serialized for each candidate, and the Worker locks the repository while completing an analysis so a concurrent removal cannot leave active evidence.

Both history endpoints return `items` and `nextCursor`. Start with `?limit=20` and pass `?limit=20&cursor=<nextCursor>` for the next page. The maximum page size is 100. Records sort newest first by creation time and ID. The cursor controls ordering and the session controls ownership. History uses the current PostgreSQL schema and includes existing stored results immediately.

## Staging verification

- On 2026-11-17 ECS reported the API revision 20 and Worker revision 13 completed with one running task per service. Both images were built from `fd924ed` with the Phase 3 restricted database roles. PostgreSQL readiness returned HTTP 200. Revisions 19 and 12 are available for rollback.
- Both synthetic accounts read and edited their own profiles, selected and removed their own repository, completed a new Worker evaluation and created a new match through the API. Profile fields and repository selections were restored after the checks. Removed selections retained their earlier evaluations. Concurrent selection of multiple repositories was covered by the integration suite.
- The paginated histories contained disjoint evaluation and match IDs for A and B. Both accounts could read older and new matches, and a cursor copied from the other account stayed scoped to the caller. The API returned 401 for anonymous history, 400 for a foreign owner in profile writes and 404 for direct foreign profile, repository selection, evaluation and match operations.
- Both synthetic job descriptions reached confirmation after a more explicit second version. Their initial extraction attempts returned `INVALID_JOB_EXTRACTION` and remain visible as failed evaluations.

The 111 backend tests passed. GitHub Actions passed backend, frontend, Playwright and both CodeQL jobs for the deployed code. Local Trivy scans of the API and Worker images found no high or critical issues. The full CI gate remains blocked by the development-only `picomatch` advisory, which has no published fix as of this verification. The production npm dependency audit reported zero vulnerabilities. [CI run](https://github.com/mausalinass/ProofPath/actions/runs/44136706985) · [Advisory](https://github.com/advisories/GHSA-fc5q-39xc-vjpf)

[PR #27](https://github.com/mausalinass/ProofPath/pull/27) builds on [Phase 3 PR #26](https://github.com/mausalinass/ProofPath/pull/26).
