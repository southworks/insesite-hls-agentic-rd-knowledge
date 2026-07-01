# Test Cases

Scenario definitions live in [`../scripts/scenarios.py`](../scripts/scenarios.py). Demo folders are built by [`build_case_folders.py`](../scripts/build_case_folders.py) into `rd-knowledge-mining/backend/dataset-seed/cases/`.

## Scenario index

| Legacy ID | Phase | Path | Final outcome | Demo folder |
|-----------|-------|------|---------------|-------------|
| `ING-001` | 1 | `full_approval` | `approved_persisted` | `cases/case-04-demo/ingest/` |
| `ING-002` | 1 | `guardrail_review` | `needs_human_review` | `cases/case-01-human-review/` |
| `ING-003` | 1 | `synthetic_provenance` | `approved_with_required_labeling` | `cases/case-02-approval-labeling/` |
| `ING-004` | 1 | `sensitive_blocked` | `denied_not_persisted` | `cases/case-03-sensitive-denied/` |
| `QRY-001` | 2 | `no_data` | `no_grounded_answer` | `cases/case-04-demo/prompts/01-no-data-prompt.txt` |
| `QRY-002` | 2 | `grounded` | `answer_with_citations` | `cases/case-04-demo/prompts/03-grounded-query-prompt.txt` |

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
