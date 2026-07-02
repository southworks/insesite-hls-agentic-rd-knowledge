# HLS Demo Dataset

Demo-ready inputs for the HLS Agentic R&D Knowledge Mining workflow. Pick a case, load policies, ingest files (if any), run the demo.

## Quick start

1. **Pick a case** under `cases/` or run the full narrative under `cases/case-04-demo/`
2. **Load** `policies/hls_policies.txt` into your RAG / embed pipeline
3. **Ingest** files from `<case>/ingest/` when the case requires upload (empty folder = no upload)

## Prerequisites

| If you want to test… | Do this first |
|---------------------|---------------|
| Headline demo (`case-04-demo`) | Run **QRY-001 → ING-001 → QRY-002** in order |
| QRY-002, QRY-003, QRY-005 (grounded / Curate paths) | Run **ING-001** so the KB is populated |
| QRY-001 (empty KB) | KB must be **empty** — do not run ING-001 first |
| Any ingestion case (`case-01` … `case-06`) | No prior scenario required |

Full dependency table: [`data-generation/docs/TEST_CASES.md`](../../../data-generation/docs/TEST_CASES.md#prerequisites-and-dependencies).

## Cases 1–6 (ingestion stress scenarios)

| Case | Folder | Legacy ID | Ingest | Expected outcome |
|------|--------|-----------|--------|------------------|
| Case 1 | `cases/case-01-human-review/` | ING-002 | Yes — 5 OA articles | Human review needed; nothing persisted |
| Case 2 | `cases/case-02-approval-labeling/` | ING-003 | Yes — synthetic ELN/LIMS | Approved with required labeling |
| Case 3 | `cases/case-03-sensitive-denied/` | ING-004 | Yes — sensitive GEO record | Denied; nothing persisted |
| Case 5 | `cases/case-05-insufficient-data/` | ING-005 | Yes — empty + truncated files | Insufficient data; nothing persisted |
| Case 6 | `cases/case-06-approve-after-review/` | ING-007 | Yes — 5 OA articles (messy pool) | Curator approves with exclusions; partial persist |

Each ingestion case folder contains:

- `README.md` — user action, ingest summary, expected outcome, legacy ID
- `ingest/` — flat files to upload

## Query scenarios (phase 2)

| Case | Folder | Legacy ID | Prompts | Expected outcome |
|------|--------|-----------|---------|------------------|
| Case 7 | `cases/case-07-eu-policy-query/` | QRY-003 | `prompts/prompt.txt` | EU policy compliance flag |
| Case 8 | `cases/case-08-clarification-query/` | QRY-004 | `prompts/prompt.txt` | Clarification needed |
| Case 9 | `cases/case-09-multi-turn-query/` | QRY-005 | `prompts/01-*.txt`, `02-*.txt` | Multi-turn grounded + Curate |

**Prerequisites:** QRY-003 and QRY-005 require **ING-001** first (populated KB). QRY-004 is best tested after ING-001. See [prerequisites table](../../../data-generation/docs/TEST_CASES.md#prerequisites-and-dependencies).

Agent capability coverage: [`TEST_CASES.md`](../../../data-generation/docs/TEST_CASES.md#what-each-scenario-tests).

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

Generation scripts, corpus, and ground truth live in [`../../../data-generation/`](../../../data-generation/). See [`../../../data-generation/README.md`](../../../data-generation/README.md#how-runtime-discovers-scenarios) to regenerate demo data or add a scenario.
