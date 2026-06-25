# HLS Demo Dataset

Demo-ready inputs for the HLS Agentic R&D Knowledge Mining workflow. Pick a case, load policies, ingest files (if any), run the demo.

## Quick start

1. **Pick a case** under `cases/` or run the full narrative under `demo-flow/`
2. **Load** `policies/hls_policies.txt` into your RAG / embed pipeline
3. **Ingest** files from `<case>/ingest/` when the case requires upload (empty folder = no upload)
4. **Send** the content of `user_input.txt` as the UI trigger (query or upload action)

## Cases 1–4 (stress scenarios)

| Case | Folder | Legacy ID | Ingest | Expected outcome |
|------|--------|-----------|--------|------------------|
| Case 1 | `cases/case-01-no-data/` | QRY-001 | No | No grounded answer (empty KB) |
| Case 2 | `cases/case-02-human-review/` | ING-002 | Yes — 5 OA articles | Human review needed; nothing persisted |
| Case 3 | `cases/case-03-approval-labeling/` | ING-003 | Yes — synthetic ELN/LIMS | Approved with required labeling |
| Case 4 | `cases/case-04-sensitive-denied/` | ING-004 | Yes — sensitive GEO record | Denied; nothing persisted |

Each case folder contains:

- `README.md` — user action, ingest summary, expected outcome, legacy ID
- `user_input.txt` — preset query or upload trigger description
- `ingest/` — files to upload (may be empty)

## Demo flow (stateful headline demo)

Run **in order** — see [`demo-flow/README.md`](demo-flow/README.md):

| Step | Folder | Legacy ID | Expected outcome |
|------|--------|-----------|------------------|
| 1 | `demo-flow/step-01-no-data/` → same as Case 1 | QRY-001 | No grounded answer |
| 2 | `demo-flow/step-02-full-approval/` | ING-001 | Clean ingest; curator approves; KB populated |
| 3 | `demo-flow/step-03-grounded-query/` | QRY-002 | Grounded answer with citations |

## Policies

All governance rules for the demo are in [`policies/hls_policies.txt`](policies/hls_policies.txt) — one file, ready to embed/load.

## Reference material

Generation scripts, corpus, entity catalogs, expected outputs, and ground truth live in [`../data-generation/`](../data-generation/). Legacy scenario IDs (`ING-*`, `QRY-*`) are preserved there for validation and rebuild.

## Team note (structural change)

Demo folders were renamed from mixed `ING-*` / `QRY-*` / `DEMO_SCENARIO/` paths to **Case 1–4** + **demo-flow**. Backend was not modified in this repo — update any hardcoded paths that pointed at `dataset-seed/00_raw/` or old scenario folder names.
