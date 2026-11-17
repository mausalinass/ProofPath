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
