# Team Handoff

The runtime demo package is `rd-knowledge-mining/backend/dataset-seed/`. It is intentionally located under the backend tree because the backend image copies it from there.

## Runtime contract

```text
rd-knowledge-mining/backend/dataset-seed/
  policies/hls_policies.txt
  cases/
    case-01-human-review/ingest/
    case-02-approval-labeling/ingest/
    case-03-sensitive-denied/ingest/
    case-04-demo/ingest/
    case-04-demo/prompts/
```

## Rebuild

```bash
cd data-generation/scripts
python3 generate_raw_layer.py
python3 build_case_folders.py
python3 generate_normalized_layers.py   # optional validation keys
```

Review changes under `rd-knowledge-mining/backend/dataset-seed/` before committing. Backend code is not changed by this data-generation workflow.

## How to add a scenario

See [`../README.md`](../README.md#how-to-add-a-scenario).
