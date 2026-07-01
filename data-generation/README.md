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
python3 build_case_folders.py       # dataset-seed/cases/*/ingest/ + prompts/
```

Optional:

```bash
python3 generate_agent_documents.py        # pdf/txt/md/html agent_inputs in corpus/
python3 generate_normalized_layers.py      # ground-truth/ only (validation answer keys)
```

No `entity-catalog/`, `expected-outputs/`, or `source/` are produced — demo data lands directly in `dataset-seed/cases/`.

## Legacy scenario IDs

Scripts and ground truth use the original IDs: `QRY-001`, `ING-001`, `QRY-002`, `ING-002`, `ING-003`, `ING-004`. Demo-facing folder names (Case 1–4) live only under `dataset-seed/`.

## Demo package

Runtime demo inputs: [`../dataset-seed/`](../dataset-seed/)
