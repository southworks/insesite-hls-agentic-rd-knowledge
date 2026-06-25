# HLS Data Generation & Reference

Everything needed to **build, validate, and rebuild** the HLS dataset — not consumed directly at demo runtime.

## Layout

| Path | Contents |
|------|----------|
| `corpus/` | Canonical raw source files (articles, trials, GEO, policies, synthetic ELN/LIMS) |
| `entity-catalog/` | Normalized JSON entities (`01_research_documents/` … `08_policy_rag/`) |
| `ground-truth/` | Full e2e answer keys per legacy scenario ID (`QRY-001.json`, `ING-002.json`, …) |
| `expected-outputs/` | Per-stage `input/`, `expected_output/`, `scenario.json` (rebuilt by scripts) |
| `source/` | Source catalog, bronze exports, scenario helpers |
| `scripts/` | Python generators and rebuild tools |
| `docs/` | HANDOFF, TEST_CASES, RAW_LAYER, TESTING_GUIDE, etc. |

## Regenerate reference artifacts

From `data-generation/scripts/`:

```bash
# Full rebuild (offline, no network for scenario folders):
python3 generate_normalized_layers.py   # entity catalog + ground truth
python3 build_scenario_folders.py       # expected-outputs/ per legacy ID

# Optional — refresh corpus from public sources (needs network):
python3 generate_raw_layer.py

# Sync demo ingest/ folders after rebuilding expected-outputs:
python3 sync_demo_ingest.py
```

## Legacy scenario IDs

Scripts and ground truth use the original IDs: `QRY-001`, `ING-001`, `QRY-002`, `ING-002`, `ING-003`, `ING-004`. Demo-facing folder names (Case 1–4, demo-flow) live only under `dataset-seed/`.

## Demo package

Runtime demo inputs: [`../dataset-seed/`](../dataset-seed/)
