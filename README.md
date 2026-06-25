# HLS Agentic R&D Knowledge Mining

Dataset and solution accelerator workspace for an HLS agentic R&D knowledge mining scenario with Cohere models on Azure.

## Scenario

This repository defines a compliance-safe dataset for an agentic research knowledge hub. The dataset starts from a raw layer of public or simulated R&D knowledge artifacts and produces downstream entities that represent how agents ingest, normalize, link, retrieve, curate, and govern research content.

HLS is **two sequential phases**, each closed by a distinct human actor (see [workflow-summary.md](workflow-summary.md)). Each is started by a controlled UI action, not a free-form chatbot:

- **Phase 1 — Ingestion & structuring** — *load* knowledge: upload documents → ingestion & translation → metadata extraction & linking → **knowledge curator approves** → persistence into the CMS/knowledge base. (Manual file upload stands in for the conceptual "Connect Portals" external connector.)
- **Phase 2 — Search & compliance** — *query* that knowledge later: UI query → search & chat retrieval (Cohere Embed/Rerank) → curation & compliance review → **compliance owner approves** → grounded answer with citations. Runs immediately after phase 1 or deferred.

The demo traverses **every** agent and both human actors, but datasets are materialized **only for the data-consuming agents** (the rest of the chain lives in each scenario ground-truth file). The headline demo is stateful: **search an empty KB → ingest → search again** and the grounded answer now appears.

- **Demo inputs:** [`dataset-seed/README.md`](dataset-seed/README.md) — Case 1–4 + demo-flow, policies, ingest files
- **Reference / rebuild:** [`data-generation/README.md`](data-generation/README.md) — corpus, entity catalog, scripts, ground truth
- **Technical docs:** [`data-generation/docs/HANDOFF.md`](data-generation/docs/HANDOFF.md), [`data-generation/docs/TEST_CASES.md`](data-generation/docs/TEST_CASES.md), [`data-generation/docs/TESTING_GUIDE.md`](data-generation/docs/TESTING_GUIDE.md)

## Dataset Direction

The raw layer is expected to represent artifacts such as research articles, protocols, ELN/LIMS-style records, datasets, results, submissions, partner or vendor repositories, and regional policy references.

The source baseline uses public healthcare and life sciences R&D sources that avoid patient-identifiable information and do not introduce compliance concerns. ELN/LIMS-style records are synthetic and derived from public source structure only.

The selected public-source baseline is documented in [docs/source-baseline.md](docs/source-baseline.md).

## Repository Scope

The initial commit intentionally included only:

- `README.md`
- `.gitignore`

Current dataset layout:

**Demo package** (`dataset-seed/`):

- `README.md` — case index and quick start
- `cases/case-01-no-data/` … `case-04-sensitive-denied/` — stress scenarios (legacy IDs in each README)
- `demo-flow/step-01-no-data/` … `step-03-grounded-query/` — stateful headline demo
- `policies/hls_policies.txt` — all governance rules in one file

**Generation & reference** (`data-generation/`):

- `corpus/` — canonical raw source files
- `entity-catalog/` — normalized JSON entities (`01_research_documents/` … `08_policy_rag/`)
- `ground-truth/` — e2e answer keys (`QRY-001.json`, `ING-001.json`, …)
- `expected-outputs/` — per-stage validation artifacts (rebuilt by scripts)
- `scripts/` — `generate_raw_layer.py`, `generate_normalized_layers.py`, `build_scenario_folders.py`, `sync_demo_ingest.py`, `scenarios.py`
- `docs/` — HANDOFF, TEST_CASES, TESTING_GUIDE, RAW_LAYER, etc.
- `source/` — source catalog and bronze exports

## Alignment

This repository is intended to follow the same scenario-driven dataset approach used by:

- `southworks/loan-mortgage-agents`
- `southworks/inesite-agentic-inventory-planning`

Future commits should keep the raw layer and derived entities explicit, auditable, and easy to trace back to the scenario narrative.
