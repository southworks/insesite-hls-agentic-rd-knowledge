# Team Handoff — Structural Change

## Before → After

| Before | After |
|--------|-------|
| `dataset-seed/00_raw/` mixed tree | Removed — demo cases are self-contained under `cases/` |
| `dataset-seed/01_*` … `09_*` entity catalogs | Removed — entities built in memory; validation in `ground-truth/` |
| `expected-outputs/` per-stage folders | Removed — `build_case_folders.py` writes directly to `dataset-seed/cases/` |
| `build_scenario_folders.py` + `sync_demo_ingest.py` | Replaced by `build_case_folders.py` |
| `source/_source/source_catalog.json` | Moved to `corpus/source_catalog.json` |

## Current demo layout

```
dataset-seed/cases/
  case-01-human-review/ingest/       ING-002
  case-02-approval-labeling/ingest/  ING-003
  case-03-sensitive-denied/ingest/   ING-004
  case-04-demo/ingest/               ING-001
  case-04-demo/prompts/              QRY-001, QRY-002
```

## Regeneration

```bash
cd data-generation/scripts
python3 generate_raw_layer.py       # corpus/
python3 build_case_folders.py       # dataset-seed/cases/
python3 generate_normalized_layers.py   # optional: ground-truth/
```

Update any runtime code that pointed at `dataset-seed/00_raw/`, `expected-outputs/`, or `entity-catalog/` to use `dataset-seed/cases/` and `data-generation/corpus/`.
