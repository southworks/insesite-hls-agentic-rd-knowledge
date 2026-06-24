# HLS Agentic R&D Knowledge Mining

Dataset and solution accelerator workspace for an HLS agentic R&D knowledge mining scenario with Cohere models on Azure.

## Scenario

This repository defines a compliance-safe dataset for an agentic research knowledge hub. The dataset starts from a raw layer of public or simulated R&D knowledge artifacts and produces downstream entities that represent how agents ingest, normalize, link, retrieve, curate, and govern research content.

HLS is **two separate processes, decoupled in time** (not one continuous flow with a single orchestrator). Each is started by a controlled UI action, not a free-form chatbot:

- **Ingestion flow** — *load* knowledge: upload documents → ingestion & translation → metadata extraction & linking → human approval → persistence into the CMS/knowledge base. (Manual file upload stands in for the conceptual "Connect Portals" external connector.)
- **Search flow** — *query* that knowledge later: UI query → search & chat retrieval (Cohere Embed/Rerank) → curation & compliance review of the result → grounded answer with citations.

The headline demo is stateful: **search an empty KB → ingest → search again** and the grounded answer now appears. See [dataset-seed/HANDOFF.md](dataset-seed/HANDOFF.md), [dataset-seed/TEST_CASES.md](dataset-seed/TEST_CASES.md), and [dataset-seed/TESTING_GUIDE.md](dataset-seed/TESTING_GUIDE.md).

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
- `dataset-seed/TESTING_GUIDE.md` (high-level demo runbook: drive each scenario by injecting the per-agent folders)
- `dataset-seed/AGENT_INPUTS.md`
- `dataset-seed/FORMAT_DECISIONS.md`
- `dataset-seed/scenarios.py`, `dataset-seed/HANDOFF.md`
- `dataset-seed/00_raw/_corpus/{csv,html,json,md,pdf,txt,xml}/` (canonical) and `dataset-seed/00_raw/{ING,QRY}-*_<path>/` (per-scenario, per-stage flow folders)
- `dataset-seed/01_*` through `dataset-seed/09_*`
- `dataset-seed/dataset-manifest.json`

## Alignment

This repository is intended to follow the same scenario-driven dataset approach used by:

- `southworks/loan-mortgage-agents`
- `southworks/inesite-agentic-inventory-planning`

Future commits should keep the raw layer and derived entities explicit, auditable, and easy to trace back to the scenario narrative.
