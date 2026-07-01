# Agent Handoff Map

Five data-consuming agents plus two human gates and persistence. Demo upload payloads live in `rd-knowledge-mining/backend/dataset-seed/cases/`; full stage contracts live in `data-generation/ground-truth/<ID>.json`.

## Phase 1 — Ingestion & structuring

| Stage | Agent | Demo input |
|-------|-------|------------|
| ingestion_translation | ingestion_translation_agent | Flat files in `<case>/ingest/` |
| metadata_linking | metadata_linking_agent | Workflow memory (KB entities) |
| curator_approval | Knowledge Curator (HITL) | Memory only |
| persistence | CMS/KB sink | Memory only |

## Phase 2 — Search & compliance

| Stage | Agent | Demo input |
|-------|-------|------------|
| search_chat | search_chat_agent | Query text in `case-04-demo/prompts/` |
| curation_compliance | curation_compliance_agent | Draft answer in memory |
| compliance_approval | Compliance Owner (HITL) | Memory only |
| response | Final output | Memory only |

## Scenario → demo folder

| Legacy ID | Demo path |
|-----------|-----------|
| ING-002 | `cases/case-01-human-review/` |
| ING-003 | `cases/case-02-approval-labeling/` |
| ING-004 | `cases/case-03-sensitive-denied/` |
| ING-001, QRY-001, QRY-002 | `cases/case-04-demo/` |

## Validation

Ground-truth rollups: `ground-truth/ING-*.json`, `ground-truth/QRY-*.json`

Each file lists every stage with `agent_input`, `expected_output`, gates, and `final_outcome`.

## Rebuild

```bash
cd data-generation/scripts
python3 generate_raw_layer.py
python3 build_case_folders.py
python3 generate_normalized_layers.py   # optional
```

## How to add a scenario

Add the scenario in `data-generation/scripts/scenarios.py`, regenerate `rd-knowledge-mining/backend/dataset-seed/`, review the data-package diff, then rebuild and redeploy. Backend code is not part of this data-generation workflow.
