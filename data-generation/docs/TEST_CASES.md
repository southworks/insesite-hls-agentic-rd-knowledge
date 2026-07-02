# Test Cases

Scenario definitions live in [`../scripts/scenarios.py`](../scripts/scenarios.py). Demo folders are built by [`build_case_folders.py`](../scripts/build_case_folders.py) into `rd-knowledge-mining/backend/dataset-seed/cases/`.

## Scenario index

| Legacy ID | Phase | Path | Final outcome | Demo folder |
|-----------|-------|------|---------------|-------------|
| `ING-001` | 1 | `full_approval` | `approved_persisted` | `cases/case-04-demo/ingest/` |
| `ING-002` | 1 | `guardrail_review` | `needs_human_review` | `cases/case-01-human-review/` |
| `ING-003` | 1 | `synthetic_provenance` | `approved_with_required_labeling` | `cases/case-02-approval-labeling/` |
| `ING-004` | 1 | `sensitive_blocked` | `denied_not_persisted` | `cases/case-03-sensitive-denied/` |
| `ING-005` | 1 | `insufficient_data` | `insufficient_data_not_persisted` | `cases/case-05-insufficient-data/` |
| `ING-007` | 1 | `approve_after_review` | `approved_with_exclusions_persisted` | `cases/case-06-approve-after-review/` |
| `QRY-001` | 2 | `no_data` | `no_grounded_answer` | `cases/case-04-demo/prompts/01-no-data-prompt.txt` |
| `QRY-002` | 2 | `grounded` | `answer_with_citations` | `cases/case-04-demo/prompts/03-grounded-query-prompt.txt` |
| `QRY-003` | 2 | `eu_policy_gap` | `flagged_for_compliance_review` | `cases/case-07-eu-policy-query/prompts/` |
| `QRY-004` | 2 | `clarification_needed` | `clarification_needed` | `cases/case-08-clarification-query/prompts/` |
| `QRY-005` | 2 | `multi_turn_curate` | `answer_with_citations` | `cases/case-09-multi-turn-query/prompts/` |

## Prerequisites and dependencies

Scenarios do **not** all depend on each other. Two rules matter for testers:

1. **KB state** — some query scenarios need an empty Vector DB; others need a populated one.
2. **Narrative order** — only the headline demo (`case-04-demo`) has a fixed step sequence.

### Prerequisites by scenario

| Legacy ID | KB state required | Run after | Notes |
|-----------|-------------------|-----------|-------|
| `ING-001` | Any (writes to KB) | — | Standalone upload; also step 2 of headline demo |
| `ING-002` | Any | — | Standalone; does **not** persist |
| `ING-003` | Any | — | Standalone |
| `ING-004` | Any | — | Standalone; curator deny |
| `ING-005` | Any | — | Standalone; insufficient-data batch |
| `ING-007` | Any | — | Standalone; **not** a follow-up to ING-002 (same pool type, separate session) |
| `QRY-001` | **Empty** | — | Run **before** ING-001 for headline demo; reset KB if re-testing |
| `QRY-002` | **Populated** | `ING-001` | Step 3 of headline demo; same query as QRY-001 |
| `QRY-003` | **Populated** | `ING-001` | EU policy gap; needs osimertinib evidence in KB |
| `QRY-004` | Populated (recommended) | `ING-001` (typical) | Clarification path; works best when KB has content but query scope is vague |
| `QRY-005` | **Populated** | `ING-001` | Two turns in **same session**, then Curate |

### Recommended test paths

**Headline demo (fixed order)** — `case-04-demo`:

```
QRY-001  →  ING-001  →  QRY-002
(empty KB)   (ingest)    (grounded query)
```

**Standalone ingestion** — pick any `case-01` … `case-06` folder; no prior scenario required.

**Standalone query (populated KB)** — run **ING-001** once (any case folder with the 5 OA articles), then pick `case-07` … `case-09` or QRY-002.

**Fresh environment** — if the Vector DB was cleared or this is a new deployment:

1. Skip QRY-001 if you only want to test populated-KB queries.
2. Run **ING-001** first.
3. Then run QRY-002, QRY-003, QRY-004, or QRY-005.

**Re-testing QRY-001** — KB must be empty. If ING-001 already ran, reset the KB / redeploy without persisted entities before step 1.

### What is *not* a dependency

- ING-007 does **not** require running ING-002 first (curator outcome differs; ingest pool is similar but sessions are independent).
- Query cases do **not** depend on each other (QRY-003 does not require QRY-002 in the same session).
- Ingestion cases do **not** chain (ING-003 does not require ING-002).

## What each scenario tests

### Phase 1 — Ingestion

| ID | Agents exercised | Key decision / gate |
|----|------------------|---------------------|
| `ING-001` | ingestion-translation, metadata-linking | Happy path; curator **approve**; full persist |
| `ING-002` | ingestion-translation (license exclusion), metadata-linking (GEO triage) | Curator **needs review**; nothing persisted |
| `ING-003` | ingestion-translation (`eln_lims`), metadata-linking | Curator **approve with labeling**; synthetic provenance |
| `ING-004` | ingestion-translation (sensitive metadata), metadata-linking (`no_action`) | Curator **deny**; patient-derived GEO blocked |
| `ING-005` | ingestion-translation, metadata-linking | **`Insufficient Data`** on empty/truncated batch; curator deny |
| `ING-007` | Same guardrails as `ING-002` | Curator **approve with exclusions**; partial persist |

### Phase 2 — Search & compliance

| ID | Agents exercised | Key decision / gate |
|----|------------------|---------------------|
| `QRY-001` | search-chat | Empty KB → `Insufficient Evidence` |
| `QRY-002` | search-chat, curation-compliance | Grounded answer; Curate **approve**; compliance approve |
| `QRY-003` | search-chat, curation-compliance | EU-scoped query; Curate **`Flag for Review`** (`HLS-REGION-EU-400`) |
| `QRY-004` | search-chat | Ambiguous query → **`Clarification Needed`** (no Curate) |
| `QRY-005` | search-chat (2 turns), curation-compliance | Multi-turn session; Curate reviews full `chatResponses` |

### Headline demo sequence

Run in order under `case-04-demo`: `QRY-001` → `ING-001` → `QRY-002`. See [Prerequisites and dependencies](#prerequisites-and-dependencies).

## Ground truth

Full e2e rollups (optional validation): `ground-truth/<ID>.json`

Each rollup includes every stage (agents, gates, persistence) with expected decisions and entity handoffs.

## Regenerate

```bash
cd data-generation/scripts
python3 generate_raw_layer.py          # corpus/ (needs network on first run)
python3 build_case_folders.py          # rd-knowledge-mining/backend/dataset-seed/cases/
python3 generate_normalized_layers.py  # ground-truth/
```

## How to add a scenario

See [`../README.md`](../README.md#how-to-add-a-scenario).
