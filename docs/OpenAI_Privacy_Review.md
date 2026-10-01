# OpenAI privacy and retention decision

Date: October 1, 2026.
Scope: ProofPath staging with synthetic résumés and synthetic job descriptions.

## Provider behavior reviewed

According to OpenAI's official API data controls documentation, API inputs and outputs are not used to train OpenAI models unless the customer explicitly opts in. Default abuse-monitoring logs may retain customer content for up to 30 days. Zero Data Retention and Modified Abuse Monitoring require prior approval from OpenAI and are not assumed by ProofPath.

ProofPath sends requests to the Responses API with `store=false`. This prevents application-state storage for the response, but it does not by itself remove the default abuse-monitoring retention described above.

## ProofPath controls verified

- The original PDF or DOCX binary is never sent to OpenAI.
- The résumé is structurally validated and malware-scanned before persistence or analysis.
- Only extracted text blocks are sent.
- Email addresses and phone numbers are removed before résumé text leaves ProofPath.
- The provider receives no candidate score, hiring decision or demographic instruction.
- Prompts and provider responses are bounded, schema-validated and are not written to application logs.
- Job descriptions contain no candidate profile when they are analyzed.

## Staging decision

Staging may use OpenAI only with synthetic, non-personal test data. The GitHub environment variable `PROOFPATH_OPENAI_PRIVACY_REVIEWED=true` means only that this synthetic-data staging decision was accepted. It must not be copied to a production environment as evidence of approval for real résumés.

## Production decision still required

Before processing real résumés, the product owner must explicitly accept the possible default retention of content for up to 30 days or obtain Zero Data Retention / Modified Abuse Monitoring approval from OpenAI. The production environment must have its own protected approval variable and a user-facing privacy notice.

Official source: https://developers.openai.com/api/docs/guides/your-data
