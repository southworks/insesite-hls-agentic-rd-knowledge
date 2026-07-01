# HLS Demo Dataset

Demo-ready inputs for the HLS Agentic R&D Knowledge Mining workflow. Pick a case, load policies, ingest files (if any), run the demo.

## Quick start

1. **Pick a case** under `cases/` or run the full narrative under `cases/case-04-demo/`
2. **Load** `policies/hls_policies.txt` into your RAG / embed pipeline
3. **Ingest** files from `<case>/ingest/` when the case requires upload (empty folder = no upload)

## Cases 1–3 (stress scenarios)

| Case | Folder | Legacy ID | Ingest | Expected outcome |
|------|--------|-----------|--------|------------------|
| Case 1 | `cases/case-01-human-review/` | ING-002 | Yes — 5 OA articles | Human review needed; nothing persisted |
| Case 2 | `cases/case-02-approval-labeling/` | ING-003 | Yes — synthetic ELN/LIMS | Approved with required labeling |
| Case 3 | `cases/case-03-sensitive-denied/` | ING-004 | Yes — sensitive GEO record | Denied; nothing persisted |

Each case folder contains:

- `README.md` — user action, ingest summary, expected outcome, legacy ID
- `ingest/` — flat files to upload

## Demo flow (stateful headline demo)

Run **in order** — see [`cases/case-04-demo/README.md`](cases/case-04-demo/README.md):

| Step | Legacy ID | Action | Location |
|------|-----------|--------|----------|
| 1 | QRY-001 | Query empty KB | `cases/case-04-demo/prompts/01-no-data-prompt.txt` |
| 2 | ING-001 | Upload & ingest clean OA articles | `cases/case-04-demo/ingest/` |
| 3 | QRY-002 | Same query — grounded answer with citations | `cases/case-04-demo/prompts/03-grounded-query-prompt.txt` |

## Policies

All governance rules for the demo are in [`policies/hls_policies.txt`](policies/hls_policies.txt) — one file, ready to embed/load.

## Reference material

Generation scripts, corpus, and ground truth live in [`../../../data-generation/`](../../../data-generation/). Legacy scenario IDs (`ING-*`, `QRY-*`) are preserved there for validation and rebuild.

## How to add a scenario

Add or modify scenarios in [`../../../data-generation/`](../../../data-generation/), not by hand-editing only this runtime package. After updating `data-generation/scripts/scenarios.py`, run `build_case_folders.py`, review the generated changes under this folder carefully, rebuild the images or deployment package that embeds it, and redeploy. Backend code is intentionally out of scope for data-generation changes.
