# Raw Layer (Corpus)

The canonical raw layer lives under `data-generation/corpus/`. It mixes real public sources and clearly marked synthetic operational records.

## Layout

```
corpus/
  source_catalog.json          scenario anchors and source list
  raw_manifest.json            file inventory with hashes
  xml/articles/pmc_oa/...      PMC OA JATS XML
  json/trials/...              ClinicalTrials.gov JSON
  json/datasets/geo/...        GEO metadata
  csv|txt/synthetic_eln_lims/  synthetic ELN/LIMS
  txt|md|html|pdf/agent_inputs/  optional multi-format evidence cards
```

## Scripts

| Script | Output |
|--------|--------|
| `generate_raw_layer.py` | Rebuilds `corpus/` from `source_catalog.json` (network) |
| `build_case_folders.py` | Slices/uploads into `rd-knowledge-mining/backend/dataset-seed/cases/*/ingest/` |
| `generate_agent_documents.py` | Optional evidence cards under `corpus/*/agent_inputs/` |
| `generate_normalized_layers.py` | Optional `ground-truth/` rollups |

## Regenerate

```bash
cd data-generation/scripts
python3 generate_raw_layer.py
python3 build_case_folders.py
```

Entities are derived in memory during ground-truth generation — there is no on-disk `entity-catalog/`.

## How to add a scenario

New scenarios are not injected into a running app. They become available only after regenerating the dataset package under `rd-knowledge-mining/backend/dataset-seed/`, rebuilding the assets that embed it, and redeploying.

1. Add or update source files and source catalog entries under `data-generation/corpus/`.
2. Add the `ING-XXX` or `QRY-XXX` scenario in `data-generation/scripts/scenarios.py`.
3. Map ingestion scenarios to `CASE_FOLDERS`; map query prompts in `DEMO_PROMPT_FILES` when the headline demo needs a prompt file.
4. Run `generate_raw_layer.py` and `build_case_folders.py`.
5. Review the generated changes under `rd-knowledge-mining/backend/dataset-seed/` before committing.
6. Run `generate_normalized_layers.py` if validation ground truth changed.
7. Rebuild and redeploy the dataset-bearing app assets.

## Privacy posture

- No patient-level data or patient names
- Synthetic people are fictional lab/reviewer names only
- GEO records are cell-line or assay-level where selected
