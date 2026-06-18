# HLS Agentic R&D Knowledge Mining

Dataset and solution accelerator workspace for an HLS agentic R&D knowledge mining scenario with Cohere models on Azure.

## Scenario

This repository defines a compliance-safe dataset for an agentic research knowledge hub. The dataset starts from a raw layer of public or simulated R&D knowledge artifacts and produces downstream entities that represent how agents ingest, normalize, link, retrieve, curate, and govern research content.

The scenario is aligned to:

- Ingestion and translation of R&D source material
- Metadata extraction, entity extraction, and version linking
- Retrieval with Cohere Embed and Cohere Rerank
- Search and chat over grounded research evidence
- Curation, compliance review, and human approval decisions

## Dataset Direction

The raw layer is expected to represent artifacts such as research articles, protocols, ELN/LIMS-style records, datasets, results, submissions, partner or vendor repositories, and regional policy references.

The source baseline uses public healthcare and life sciences R&D sources that avoid patient-identifiable information and do not introduce compliance concerns. ELN/LIMS-style records are synthetic and derived from public source structure only.

The selected public-source baseline is documented in [docs/source-baseline.md](docs/source-baseline.md).

## Repository Scope

The initial commit intentionally included only:

- `README.md`
- `.gitignore`

Current dataset planning and raw-source materials include:

- `docs/source-baseline.md`
- `dataset-seed/_source/source_catalog.json`
- `dataset-seed/generate_raw_layer.py`
- `dataset-seed/generate_normalized_layers.py`
- `dataset-seed/generate_agent_documents.py`
- `dataset-seed/build_scenario_folders.py`
- `dataset-seed/RAW_LAYER.md`
- `dataset-seed/TEST_CASES.md`
- `dataset-seed/AGENT_INPUTS.md`
- `dataset-seed/FORMAT_DECISIONS.md`
- `dataset-seed/00_raw/_corpus/{csv,html,json,md,pdf,txt,xml}/` (canonical) and `dataset-seed/00_raw/GT-*/` (per-scenario)
- `dataset-seed/01_*` through `dataset-seed/09_*`
- `dataset-seed/dataset-manifest.json`

## Alignment

This repository is intended to follow the same scenario-driven dataset approach used by:

- `southworks/loan-mortgage-agents`
- `southworks/inesite-agentic-inventory-planning`

Future commits should keep the raw layer and derived entities explicit, auditable, and easy to trace back to the scenario narrative.
