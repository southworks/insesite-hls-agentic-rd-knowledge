# Team handoff — HLS dataset structure change

**Date:** 2025-06-25  
**Repo:** `hls-agentic-rd-knowledge-mining`  
**Backend:** not modified in this repo

## What changed

| Before | After |
|--------|-------|
| `dataset-seed/00_raw/` (corpus + scenarios mixed in) | Removed from demo package |
| `dataset-seed/01_*` … `09_*` entity catalogs | `data-generation/entity-catalog/` |
| Mixed folder names (`ING-002_guardrail_review`, `DEMO_SCENARIO/1-QRY-001_…`) | `dataset-seed/cases/case-01` … `case-04` + `demo-flow/step-0N` |
| Policies as JSON in `08_policy_rag/` | Single `dataset-seed/policies/hls_policies.txt` |
| Scripts in `dataset-seed/` | `data-generation/scripts/` |

## Legacy IDs preserved

`QRY-001`, `ING-001`, `QRY-002`, `ING-002`, `ING-003`, `ING-004` remain in:

- Each case `README.md` under `dataset-seed/`
- `data-generation/ground-truth/*.json`
- `data-generation/scripts/scenarios.py`

## Action for integration teams

If any runtime code pointed at old paths (`dataset-seed/00_raw/`, `ING-*_guardrail_review/`, etc.), update to the new demo layout documented in [`dataset-seed/README.md`](../dataset-seed/README.md).

## Case mapping

| Case | Legacy ID |
|------|-----------|
| Case 1 — no data | QRY-001 |
| Case 2 — human review | ING-002 |
| Case 3 — approval labeling | ING-003 |
| Case 4 — sensitive denied | ING-004 |
| Demo step 2 — full approval | ING-001 |
| Demo step 3 — grounded query | QRY-002 |
