# Raw Layer - HLS Agentic R&D Knowledge Mining Dataset Seed

Tracks the structure, source data, generation logic, and compliance posture of
the Raw Layer (`00_raw/`). Update this file whenever a new source family,
document type, trial, article, dataset, or synthetic operational record is added.

## What is the Raw Layer?

The Raw Layer contains the source artifacts ingested by the HLS agentic research
knowledge hub. It comes before any normalized entity folders.

```text
Public source documents + synthetic ELN/LIMS records
  -> 00_raw/
  -> ingestion, metadata/linking, retrieval, curation, compliance agents
  -> normalized JSON entity layers in later commits
```

This repository contains the Raw Layer plus derived normalized entity folders.
The Raw Layer remains the source of truth for regeneration.

## Source Data

The scenario is centered on **EGFR-mutated non-small cell lung cancer (NSCLC)**
and **osimertinib / Tagrisso / AZD9291**. This gives us a coherent evidence
graph across clinical trials, articles, experimental datasets, compound/target
registries, regulatory submissions, policy documents, and lab-style operational
records.

Source catalog:

- [`_source/source_catalog.json`](_source/source_catalog.json)

Selected public source families:

| Source family | Raw representation | Public source |
| --- | --- | --- |
| Research articles | `xml/articles/`, `json/articles/` | PMC Open Access / Europe PMC |
| Clinical trials | `json/trials/`, `pdf/trials/` | ClinicalTrials.gov API and CDN |
| Experimental datasets | `json/datasets/`, `txt/datasets/` | NCBI GEO |
| Compound/target registry | `json/registries/` | ChEMBL |
| US regulatory | `json/regulatory/`, `html/regulatory/`, `pdf/regulatory/` | openFDA and Drugs@FDA |
| EU regulatory | `html/regulatory/`, `json/regulatory/` | EMA |
| Region policies | `html/policies/`, `json/policies/` | FDA, EU, ClinicalTrials.gov, AACT |
| Partner/vendor repositories | `csv/partner_vendor_repositories/` | Synthetic from public source catalog |
| ELN/LIMS | `txt/synthetic_eln_lims/`, `csv/synthetic_eln_lims/` | Synthetic from public source structure |

## Folder Structure - organized by scenario / test case

The Raw Layer is organized **by scenario / test case, not by format**. Because HLS is
*entity-based* (the same entity - e.g. `RDOC-PMC6889286`, `TRIAL-NCT02296125` - is cited by
more than one case), a strict per-scenario partition requires **duplication**. So the layout
keeps one canonical copy and adds self-contained per-scenario folders:

```text
dataset-seed/
|-- _source/
|   `-- source_catalog.json
|-- 00_raw/
|   |-- _corpus/                 <- CANONICAL: the single source of truth (all source files)
|   |   |-- json/  xml/  pdf/  html/  txt/  csv/  md/   <- by source family + format, as fetched
|   |   |   |   articles/pmc_oa/<PMCID>/...   trials/clinicaltrials_gov/<NCT_ID>/...
|   |   |   |   datasets/geo/<GSE>/...        registries/chembl/...   regulatory/<SOURCE_ID>/...
|   |   |   |   policies/<SOURCE_ID>/...      synthetic_eln_lims/...  partner_vendor_repositories/...
|   |   |   `-- <fmt>/agent_inputs/<ENTITY_CATEGORY>/<DOCUMENT_ID>.<fmt>   <- multi-format replicas
|   |   |-- agent_document_manifest.json
|   |   `-- raw_manifest.json
|   |-- DEMO_SCENARIO/            <- headline stateful demo, numbered in run order:
|   |   |-- 1-QRY-001_no_data/        search an empty KB
|   |   |-- 2-ING-001_full_approval/  ingest
|   |   `-- 3-QRY-002_grounded/       search again (answer appears)
|   |-- ING-002_guardrail_review/ <- standalone phase-1 guardrail variants
|   |-- ING-003_synthetic_provenance/
|   `-- ING-004_sensitive_blocked/
|-- 01_research_documents/ .. 09_decision_ground_truth/   <- trimmed normalized entity catalog
|-- scenarios.py                  <- single source of truth for the ING-* / QRY-* scenarios
|-- generate_raw_layer.py         <- fetches/synthesizes the corpus into 00_raw/_corpus/
|-- build_scenario_folders.py     <- (offline) builds the scenario folders from the catalog + _corpus/
`-- generate_agent_documents.py   <- writes the multi-format replicas into _corpus/
```

HLS is **two sequential phases** (see [HANDOFF.md](HANDOFF.md)). The demo traverses every agent and
both human actors, but only the **data-consuming** agents (+ the response) get a materialized folder;
each is numbered in run order:

```text
00_raw/.../2-ING-001_full_approval/           00_raw/.../3-QRY-002_grounded/
  01_ingestion_translation/  (agent)            01_search_chat/          (agent)
  02_metadata_linking/       (agent)            02_curation_compliance/  (agent)
  scenario.json                                 03_response/  response.json
                                                scenario.json
  # curator approval + persistence:             # compliance approval:
  #   memory stages in scenario.json            #   memory stage in scenario.json
```

An *agent* stage has `agent_input.json` + `input/` + `expected_output/`; the *response* stage has
`response.json`. The human approvals and persistence carry **no folder** — they are the
`materialized: false` entries in `scenario.json` → `stages[]` (mirror of
`09_decision_ground_truth/<ID>.json`).

**Single source of truth:** `00_raw/_corpus/` is the canonical raw corpus (`raw_manifest.json` and
every normalized entity's `raw_sources` point there); the root catalog `01_*..07_*` is the canonical
entity set. The scenario folders are deliberate duplicates so each stage can be started in isolation;
they are rebuilt offline by `build_scenario_folders.py` from [`scenarios.py`](scenarios.py).
See [TEST_CASES.md](TEST_CASES.md), [TESTING_GUIDE.md](TESTING_GUIDE.md), and [HANDOFF.md](HANDOFF.md).

Current generated public/synthetic source artifact summary:

| Format area | Source file count |
| --- | ---: |
| `json/` | 49 |
| `xml/` | 10 |
| `pdf/` | 10 |
| `html/` | 6 |
| `txt/` | 7 |
| `csv/` | 2 |
| `raw_manifest.json` | 1 |
| **Total** | **85 files including manifest** |

`raw_manifest.json` records 84 public/synthetic source files plus the manifest
itself, with SHA-256 hashes and byte counts for provenance checks.

## Multi-format Agent Inputs

The HLS Raw Layer already includes mixed public-source formats. For extraction
consistency testing, compact evidence cards are generated as first-level
format folders under `00_raw/`, matching the convention used by the other
scenario repos:

```bash
cd dataset-seed
python3 generate_agent_documents.py
```

This produces 41 evidence cards across 4 formats (in the canonical corpus):

```text
00_raw/_corpus/txt/agent_inputs/<category>/<document_id>.txt
00_raw/_corpus/md/agent_inputs/<category>/<document_id>.md
00_raw/_corpus/html/agent_inputs/<category>/<document_id>.html
00_raw/_corpus/pdf/agent_inputs/<category>/<document_id>.pdf
```

The cross-format manifest is `00_raw/_corpus/agent_document_manifest.json`. These replicas are
cross-cutting consistency-test assets, so they stay in `_corpus/` (not the per-scenario folders).

See [AGENT_INPUTS.md](AGENT_INPUTS.md) and
[FORMAT_DECISIONS.md](FORMAT_DECISIONS.md).

## Test Cases

HLS is two sequential phases. The scenario rollups live in `09_decision_ground_truth/ING-*.json`
(phase 1, ingestion) and `09_decision_ground_truth/QRY-*.json` (phase 2, search), each a full e2e
answer key. The data-consuming stages are built into `00_raw/DEMO_SCENARIO/<n>-<ID>_<path>/` (the
headline demo) and `00_raw/<ID>_<path>/` (the standalone guardrail variants). Use
[TEST_CASES.md](TEST_CASES.md), [TESTING_GUIDE.md](TESTING_GUIDE.md) and [HANDOFF.md](HANDOFF.md) to
understand each scenario's stages and trace each stage's `output_entities` back to concrete files
under `00_raw/`.

## Generation Script

```bash
cd dataset-seed
python3 generate_raw_layer.py
```

The script:

1. Reads `_source/source_catalog.json`.
2. Rebuilds `00_raw/_corpus/` from scratch.
3. Downloads public source artifacts using official APIs, public CDN links, or
   official public pages.
4. Generates synthetic ELN/LIMS and partner repository records from public
   source structure.
5. Writes `00_raw/_corpus/raw_manifest.json` with hashes and file sizes.

`generate_raw_layer.py` uses Python standard library modules and `curl` as a
fallback for public sources that reject local Python SSL/user-agent behavior.

Recommended full regeneration order:

```bash
cd dataset-seed
python3 generate_raw_layer.py          # fetch/synthesize -> 00_raw/_corpus/
python3 generate_normalized_layers.py  # trimmed root catalog + 09 rollups from _corpus/
python3 generate_agent_documents.py    # multi-format replicas in _corpus/
python3 build_scenario_folders.py      # (offline) rebuild 00_raw/DEMO_SCENARIO/ + ING-* from catalog + _corpus/
```

`build_scenario_folders.py` is offline and deterministic - re-run it any time the
canonical corpus or the scenario definitions in `scenarios.py` change to refresh the
per-scenario / per-agent folders.

## Source Records

Every downloaded source folder includes a `source_record.json` that captures:

- source identifier,
- public URL/API endpoint,
- local document path where applicable,
- raw-layer policy for that source.

This lets later normalized entities trace back to raw evidence without relying
on file names alone.

## Document and File Types

| Folder | Formats | Simulates / Represents |
| --- | --- | --- |
| `articles/pmc_oa/` | XML, JSON, XML license | Research article ingestion and metadata extraction |
| `trials/clinicaltrials_gov/` | JSON, PDF | Trial registry record, protocol, statistical analysis plan |
| `datasets/geo/` | JSON, TXT | Experimental dataset metadata and sample/source descriptions |
| `registries/chembl/` | JSON | Compound, target, and mechanism registry records |
| `regulatory/` | JSON, HTML, PDF | FDA/openFDA/Drugs@FDA/EMA label, application, review, approval, EPAR sources |
| `policies/` | HTML, JSON source records | Regional policy and API/source documentation |
| `synthetic_eln_lims/` | TXT, CSV | Lab notebook, LIMS sample manifest, data stewardship QC report |
| `partner_vendor_repositories/` | CSV | Repository intake/export used by ingestion and curation agents |

## Selected Source IDs

Clinical trials:

- `NCT02296125` - FLAURA
- `NCT02151981` - AURA3
- `NCT02511106` - ADAURA
- `NCT04035486` - FLAURA2

PMC OA articles:

- `PMC6889286`
- `PMC5447962`
- `PMC13070087`
- `PMC13129538`
- `PMC13143971`

GEO datasets:

- `GSE323366`
- `GSE323365`
- `GSE272182`
- `GSE300311`
- `GSE298111`

Registry and regulatory anchors:

- `CHEMBL3353410` - osimertinib
- `CHEMBL203` - EGFR / ERBB1 target
- `NDA208065` - Tagrisso / osimertinib

## Synthetic Records

The ELN/LIMS records are synthetic by design and must stay labeled as such.
They use fictional lab/reviewer names and public source identifiers only.

Synthetic files:

- `synthetic_eln_lims/lims_sample_manifest.csv`
- `synthetic_eln_lims/eln_experiment_notebook.txt`
- `synthetic_eln_lims/lims_quality_control_report.txt`
- `partner_vendor_repositories/partner_vendor_repository_index.csv`

These records bridge the proposal's ELN/LIMS and partner/vendor repository
inputs without importing private lab systems, vendor portals, or patient-level
records.

## Compliance and PHI Posture

The Raw Layer intentionally excludes:

- patient-level datasets,
- patient names,
- real-world patient trajectories,
- case report narratives,
- GEO records whose sample titles reference patient specimens,
- articles whose full-text reuse license is unclear.

Explicitly excluded candidates are recorded in `_source/source_catalog.json`.

Before adding new raw files:

1. Prefer public aggregate or cell-line/assay-level sources.
2. Check article license before storing full text.
3. Do not add real patient identifiers, masked patient identifiers, medical
   record numbers, treatment dates, or case-report narratives.
4. Mark any generated ELN/LIMS or repository export as synthetic.
5. Keep every generated downstream entity traceable to at least one raw source.

## Normalized Entity Layers

The normalized entity folders are derived from `00_raw/` by
`generate_normalized_layers.py`, following the same convention used in the
existing repos:

```text
00_raw/_corpus/
  -> 01_research_documents/     (RDOC-* — 5)
  -> 02_clinical_trials/        (TRIAL-* — 2: FLAURA, AURA3)
  -> 03_experimental_datasets/  (DATASET-GSE* — 5, + SYN-LIMS-* — 2)
  -> 04_regulatory_submissions/ (REG-NDA208065, LBL-TAGRISSO-OPENFDA)
  -> 05_compounds_targets/      (CMP-CHEMBL3353410, TGT-CHEMBL203)
  -> 06_evidence_links/         (LINK-* — 2)
  -> 07_curation_decisions/     (CUR-EXCLUDE-* — 3)
  -> 08_policy_rag/             (HLS-* — 6)
  -> 09_decision_ground_truth/  (ING-*/QRY-* rollups — 6)
```

The catalog is **trimmed to only the entities the demo's data-consuming agents use** (noise
reduction): no per-sample GEO entities (`SAMPLE-GSM*`), no biomarker catalog (`BMK-*`), no extra
trials, links, or regulatory source documents. Each normalized folder includes a `SCHEMA.md`. The
rollup manifest is `dataset-manifest.json`.
