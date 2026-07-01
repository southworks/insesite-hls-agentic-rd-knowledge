# HLS Data Generation & Reference

Everything needed to **build, validate, and rebuild** the HLS dataset — not consumed directly at demo runtime.

## Layout

```
data-generation/
  corpus/                    canonical raw source files + source_catalog.json
  ground-truth/              optional e2e answer keys (QRY-001.json, ING-002.json, …)
  scripts/                   Python generators and build_case_folders.py
  docs/                      HANDOFF, TEST_CASES, RAW_LAYER, TESTING_GUIDE, etc.
```

## Regenerate demo cases

From `data-generation/scripts/`:

```bash
python3 generate_raw_layer.py       # corpus/ (needs network on first run)
python3 build_case_folders.py       # rd-knowledge-mining/backend/dataset-seed/cases/*/ingest/ + prompts/
```

Optional:

```bash
python3 generate_agent_documents.py        # pdf/txt/md/html agent_inputs in corpus/
python3 generate_normalized_layers.py      # ground-truth/ only (validation answer keys)
```

Demo data lands directly in `rd-knowledge-mining/backend/dataset-seed/cases/`.

## Legacy scenario IDs

Scripts and ground truth use the original IDs: `QRY-001`, `ING-001`, `QRY-002`, `ING-002`, `ING-003`, `ING-004`. Demo-facing folder names (Case 1–4) live under `rd-knowledge-mining/backend/dataset-seed/`.

## Demo package

Runtime demo inputs: [`../rd-knowledge-mining/backend/dataset-seed/`](../rd-knowledge-mining/backend/dataset-seed/)

## How to add a scenario

New scenarios are generated into the runtime dataset package and only affect the running app after the dataset-bearing images or deployment packages are rebuilt and redeployed.

1. Add or update source files and source catalog entries under `corpus/`.
2. Add the `ING-XXX` or `QRY-XXX` scenario in `scripts/scenarios.py`.
3. Map ingestion scenarios to `CASE_FOLDERS`; map query prompts in `DEMO_PROMPT_FILES` for the headline demo.
4. Run the generation commands above.
5. Review the generated changes under `../rd-knowledge-mining/backend/dataset-seed/` before committing.
6. Run `generate_normalized_layers.py` if validation ground truth changed.
7. Rebuild and redeploy the assets that embed the dataset package.
