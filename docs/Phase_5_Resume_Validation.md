# Phase 5 résumé validation

## Upload and analysis

- The staging baseline accepted a synthetic PDF with `application/pdf` and rejected the identical bytes with `application/octet-stream`. The fixed API accepts missing or generic media types after verifying the extension and bytes. It stores the canonical type and uses it for authenticated downloads.
- Preflight rejects mismatched formats, malformed PDF footer offsets, blocked PDF names, and invalid or unsafe DOCX containers before upload. Invalid inputs return file-specific error codes. ClamAV requires a complete NUL-terminated verdict and remains mandatory in staging.
- The Worker parses the document and validates the extracted facts against their cited source. A text-only PDF can complete without a generic partial-analysis warning. Pages with images or no readable text receive specific warnings. A textless document fails before any provider extraction.
- The résumé remains private throughout upload, review and confirmation. Manual confirmation is required for a new version to become active evidence.

The API accepts files up to 10 MiB. OCR remains outside the current workflow. Review the result for reading order and supporting citations on complex layouts.

## Staging evidence

On 2026-11-17, ECS finished deploying API revision 21 and Worker revision 14 from `fed4cb1`, with one running task per service. The readiness endpoint returned HTTP 200 with PostgreSQL healthy. API revision 20 and Worker revision 13 remain available for rollback.

- Accounts A and B each uploaded one synthetic PDF and one synthetic DOCX. The four jobs completed with Python evidence grounded in the original text. The matrix included a canonical media type, missing MIME metadata and `application/octet-stream`. All four downloads carried the canonical type and matched the uploaded bytes.
- Each account edited and confirmed both of its synthetic versions. The last confirmed version became active and the previous confirmation stayed in history.
- Each account received the expected rejection for a corrupt PDF, an escaped PDF script marker, a renamed file, a DOCX with a macro marker, an empty file, a mismatched explicit MIME type and an upload above the limit. These uploads created no résumé records. Each textless PDF reached the Worker and failed with `NO_EXTRACTABLE_TEXT` without publishing an extraction.
- Cross-account metadata, downloads, draft review, jobs and confirmation returned HTTP 404. The other account's documents were absent from the version list.

All 116 backend tests passed. The API and Worker images passed local Trivy scans for high and critical vulnerabilities. The remote backend, frontend, Playwright and CodeQL jobs passed. The full CI gate remains blocked by the existing `picomatch` development dependency advisory. The production npm dependency audit reported zero vulnerabilities. [CI run](https://github.com/mausalinass/ProofPath/actions/runs/44195210103) · [Advisory](https://github.com/advisories/GHSA-fc5q-39xc-vjpf)

[PR #28](https://github.com/mausalinass/ProofPath/pull/28) builds on Phase 3 and Phase 4.
