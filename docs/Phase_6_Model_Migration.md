# Phase 6 model migration

Résumé and job-description extraction use GPT-6 Luna with medium reasoning by default. The Worker records the model returned by the Responses API and stores token counts with each extraction. The prompts now require contiguous quotes from an identified source block. Server-side grounding, strict schemas, résumé redaction and explicit user confirmation remain part of the workflow. Matching uses the deterministic `matching-v1` engine.

`OpenAI:Model` and `OpenAI:ReasoningEffort` can override the defaults. For a staging rollback, restore the preceding Worker task definition or set `OpenAI__Model=gpt-6-astra` and redeploy the Worker. Previous extraction records retain their stored model, prompt version and token counts.

## Staging verification and cost comparison

On 2026-11-17 the staging Worker revision 15, built from `94687e0`, completed its rollout with one running task. The API remained on revision 21 and readiness returned HTTP 200 with PostgreSQL healthy. Worker revision 14 is the rollback point.

Accounts A and B each used the same synthetic résumé text and offer description before and after the migration. Account A used a DOCX and account B used a PDF. All four extractions per model completed on the first attempt with medium reasoning. Both versions retained the required skill, the preferred requirement and exact source citations. The matches were stored with scoring version `matching-v1`. Account A scored 57.51 and B scored 55.71 under both models. The preferred skill was `NotEvaluated` in both cases. A repeat calculation with Luna's confirmed data reproduced each score and requirement classification.

| Workload | Astra input/output tokens | Astra estimated USD | Luna input/output tokens | Luna estimated USD |
| --- | ---: | ---: | ---: | ---: |
| Two résumés | 1,165 / 486 | $0.03595 | 1,029 / 597 | $0.0004014 |
| Two job descriptions | 1,736 / 468 | $0.04076 | 1,642 / 628 | $0.0004782 |
| Total | 2,901 / 954 | $0.07671 | 2,671 / 1,225 | $0.0008796 |

The estimate multiplies recorded token counts by standard OpenAI API rates, $10 input and $50 output per million for Astra, $0.10 input and $0.50 output per million for Luna. All requests were below the cache threshold. This sample cost 98.85% less with Luna. The figures cover OpenAI extraction charges for the synthetic jobs. Matching is computed locally. [Astra pricing](https://developers.openai.com/api/docs/models/gpt-6-astra) · [Luna pricing](https://developers.openai.com/api/docs/models/gpt-6-luna) · [Prompt caching](https://developers.openai.com/api/docs/guides/prompt-caching)

All 117 backend tests passed. Local Trivy found no high or critical vulnerabilities in the Worker image. Backend, frontend, Playwright and both CodeQL jobs passed in GitHub Actions. The full CI gate remains blocked by the existing development dependency advisory. [CI run](https://github.com/mausalinass/ProofPath/actions/runs/44198789422) · [picomatch advisory](https://github.com/advisories/GHSA-fc5q-39xc-vjpf)

[PR #29](https://github.com/mausalinass/ProofPath/pull/29) builds on the preceding phase PRs.
