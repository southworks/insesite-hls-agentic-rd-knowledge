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

Run in order under `case-04-demo`: `QRY-001` → `ING-001` → `QRY-002`.

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
