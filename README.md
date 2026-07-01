# HLS Agentic R&D Knowledge Mining

Dataset and solution accelerator workspace for an HLS agentic R&D knowledge mining scenario with Cohere models on Azure.

## Scenario

This repository defines a compliance-safe dataset for an agentic research knowledge hub. The dataset starts from a raw layer of public or simulated R&D knowledge artifacts and produces downstream entities that represent how agents ingest, normalize, link, retrieve, curate, and govern research content.

HLS is **two sequential phases**, each closed by a distinct human actor (see [workflow-summary.md](workflow-summary.md)). Each is started by a controlled UI action, not a free-form chatbot:

- **Phase 1 — Ingestion & structuring** — upload documents → ingestion & translation → metadata extraction & linking → **knowledge curator approves** → persistence into the CMS/knowledge base.
- **Phase 2 — Search & compliance** — UI query → search & chat retrieval → curation & compliance review → **compliance owner approves** → grounded answer with citations.

The headline demo is stateful: **search an empty KB → ingest → search again** and the grounded answer now appears.

- **Demo inputs:** [`dataset-seed/README.md`](dataset-seed/README.md) — Case 1–4, policies, ingest files
- **Reference / rebuild:** [`data-generation/README.md`](data-generation/README.md) — corpus, scripts, ground truth
- **Technical docs:** [`data-generation/docs/HANDOFF.md`](data-generation/docs/HANDOFF.md), [`data-generation/docs/TEST_CASES.md`](data-generation/docs/TEST_CASES.md), [`data-generation/docs/TESTING_GUIDE.md`](data-generation/docs/TESTING_GUIDE.md)

## Repository layout

**Demo package** (`dataset-seed/`):

- `cases/case-01-human-review/` … `case-04-demo/` — flat `ingest/` + demo `prompts/`
- `policies/hls_policies.txt` — governance rules

**Generation & reference** (`data-generation/`):

- `corpus/` — canonical raw source files + `source_catalog.json`
- `ground-truth/` — optional e2e answer keys (`QRY-001.json`, `ING-001.json`, …)
- `scripts/` — `generate_raw_layer.py`, `build_case_folders.py`, `generate_normalized_layers.py`, `scenarios.py`
- `docs/` — handoff, testing guide, schemas

Regenerate demo cases:

```bash
cd data-generation/scripts
python3 generate_raw_layer.py
python3 build_case_folders.py
```

## Alignment

This repository follows the same scenario-driven dataset approach used by `loan-mortgage-agents` and `inesite-agentic-inventory-planning`.
