# Test Cases - Decision Ground Truth

This document explains how to read the HLS dataset seed evaluation cases and
trace each expected outcome back to concrete Raw Layer files.

## Where The Cases Live

The five evaluation cases are JSON files in:

```text
09_decision_ground_truth/
```

Each case includes:

- `scenario_id`: stable test case identifier.
- `expected_agent`: the agent capability being evaluated.
- `expected_decision`: the expected high-level result.
- `source_entities`: normalized entities that must be used as evidence.
- `expected_outputs`: measurable output expectations.

The `raw_sources` field inside the `GT-*` files points to
`00_raw/raw_manifest.json` because the ground-truth file itself is the
evaluation case. To find the concrete Raw Layer inputs, follow
`source_entities`.

## How To Trace A Case To Raw Files

Use this path:

```text
09_decision_ground_truth/GT-*.json
  -> source_entities[]
  -> matching normalized entity in 01_* through 08_*
  -> that entity's raw_sources[]
  -> concrete files under 00_raw/
```

Example:

```text
09_decision_ground_truth/GT-ANSWER-GROUNDED-QUERY.json
  -> source_entities: RDOC-PMC6889286
  -> 01_research_documents/RDOC-PMC6889286.json
  -> raw_sources:
     - 00_raw/xml/articles/pmc_oa/PMC6889286/article.xml
     - 00_raw/json/articles/pmc_oa/PMC6889286/europe_pmc_metadata.json
     - 00_raw/xml/articles/pmc_oa/PMC6889286/pmc_oa_license.xml
```

## Case Index

| Case | Expected agent | Expected decision | Expected outputs |
| --- | --- | --- | --- |
| `GT-INGEST-ARTICLES` | `ingestion_translation_agent` | `approve` | 5 accepted articles, 1 denied article |
| `GT-LINK-TRIAL-REGULATORY` | `metadata_linking_agent` | `approve` | Required links: `LINK-FLAURA-REG-NDA208065`, `LINK-REG-NDA208065-LBL-TAGRISSO-OPENFDA` |
| `GT-USE-CELL-LINE-DATASETS` | `metadata_linking_agent` | `approve` | 5 accepted GEO datasets, 2 excluded GEO datasets |
| `GT-REQUIRE-SYNTHETIC-PROVENANCE` | `curation_compliance_agent` | `approve_with_required_labeling` | Synthetic records must include `synthetic_from_public_structure` provenance |
| `GT-ANSWER-GROUNDED-QUERY` | `search_chat_agent` | `answer_with_citations` | At least 2 citations and a Raw Layer source trace |

## Case Details

### GT-INGEST-ARTICLES

Purpose: verify that selected article full text can be ingested only when the
PMC OA license is acceptable and the source is not a patient-level case report.

Expected result:

- `expected_decision`: `approve`
- `accepted_article_count`: `5`
- `denied_article_count`: `1`

Source entities and Raw Layer files:

| Source entity | Normalized entity | Raw Layer files |
| --- | --- | --- |
| `RDOC-PMC6889286` | `01_research_documents/RDOC-PMC6889286.json` | `00_raw/xml/articles/pmc_oa/PMC6889286/article.xml`; `00_raw/json/articles/pmc_oa/PMC6889286/europe_pmc_metadata.json`; `00_raw/xml/articles/pmc_oa/PMC6889286/pmc_oa_license.xml` |
| `RDOC-PMC5447962` | `01_research_documents/RDOC-PMC5447962.json` | `00_raw/xml/articles/pmc_oa/PMC5447962/article.xml`; `00_raw/json/articles/pmc_oa/PMC5447962/europe_pmc_metadata.json`; `00_raw/xml/articles/pmc_oa/PMC5447962/pmc_oa_license.xml` |
| `RDOC-PMC13070087` | `01_research_documents/RDOC-PMC13070087.json` | `00_raw/xml/articles/pmc_oa/PMC13070087/article.xml`; `00_raw/json/articles/pmc_oa/PMC13070087/europe_pmc_metadata.json`; `00_raw/xml/articles/pmc_oa/PMC13070087/pmc_oa_license.xml` |
| `RDOC-PMC13129538` | `01_research_documents/RDOC-PMC13129538.json` | `00_raw/xml/articles/pmc_oa/PMC13129538/article.xml`; `00_raw/json/articles/pmc_oa/PMC13129538/europe_pmc_metadata.json`; `00_raw/xml/articles/pmc_oa/PMC13129538/pmc_oa_license.xml` |
| `RDOC-PMC13143971` | `01_research_documents/RDOC-PMC13143971.json` | `00_raw/xml/articles/pmc_oa/PMC13143971/article.xml`; `00_raw/json/articles/pmc_oa/PMC13143971/europe_pmc_metadata.json`; `00_raw/xml/articles/pmc_oa/PMC13143971/pmc_oa_license.xml` |

The denied article is represented as a curation decision:

- `08_curation_decisions/CUR-EXCLUDE-PMC4771182.json`

### GT-LINK-TRIAL-REGULATORY

Purpose: verify that trial and regulatory entities link through shared
osimertinib / Tagrisso / AZD9291 identifiers.

Expected result:

- `expected_decision`: `approve`
- required links:
  - `07_evidence_links/LINK-FLAURA-REG-NDA208065.json`
  - `07_evidence_links/LINK-REG-NDA208065-LBL-TAGRISSO-OPENFDA.json`

Source entities and Raw Layer files:

| Source entity | Normalized entity | Raw Layer files |
| --- | --- | --- |
| `TRIAL-NCT02296125` | `02_clinical_trials/TRIAL-NCT02296125.json` | `00_raw/json/trials/clinicaltrials_gov/NCT02296125/study.json`; `00_raw/pdf/trials/clinicaltrials_gov/NCT02296125/Prot_000.pdf`; `00_raw/pdf/trials/clinicaltrials_gov/NCT02296125/SAP_001.pdf` |
| `TRIAL-NCT02151981` | `02_clinical_trials/TRIAL-NCT02151981.json` | `00_raw/json/trials/clinicaltrials_gov/NCT02151981/study.json`; `00_raw/pdf/trials/clinicaltrials_gov/NCT02151981/Prot_000.pdf`; `00_raw/pdf/trials/clinicaltrials_gov/NCT02151981/SAP_001.pdf` |
| `REG-NDA208065` | `05_regulatory_submissions/REG-NDA208065.json` | `00_raw/json/regulatory/OPENFDA_DRUGSFDA_NDA208065/OPENFDA_DRUGSFDA_NDA208065.json` |
| `LBL-TAGRISSO-OPENFDA` | `05_regulatory_submissions/LBL-TAGRISSO-OPENFDA.json` | `00_raw/json/regulatory/OPENFDA_LABEL_TAGRISSO/OPENFDA_LABEL_TAGRISSO.json` |

### GT-USE-CELL-LINE-DATASETS

Purpose: verify that the selected experimental datasets are cell-line or
assay-level sources and that patient-derived candidates remain excluded.

Expected result:

- `expected_decision`: `approve`
- `accepted_geo_count`: `5`
- `excluded_geo_count`: `2`

Source entities and Raw Layer files:

| Source entity | Normalized entity | Raw Layer files |
| --- | --- | --- |
| `DATASET-GSE323366` | `03_experimental_datasets/DATASET-GSE323366.json` | `00_raw/json/datasets/geo/GSE323366/geo_esummary.json`; `00_raw/txt/datasets/geo/GSE323366/series_soft.txt`; `00_raw/json/datasets/geo/GSE323366/source_record.json` |
| `DATASET-GSE323365` | `03_experimental_datasets/DATASET-GSE323365.json` | `00_raw/json/datasets/geo/GSE323365/geo_esummary.json`; `00_raw/txt/datasets/geo/GSE323365/series_soft.txt`; `00_raw/json/datasets/geo/GSE323365/source_record.json` |
| `DATASET-GSE272182` | `03_experimental_datasets/DATASET-GSE272182.json` | `00_raw/json/datasets/geo/GSE272182/geo_esummary.json`; `00_raw/txt/datasets/geo/GSE272182/series_soft.txt`; `00_raw/json/datasets/geo/GSE272182/source_record.json` |
| `DATASET-GSE300311` | `03_experimental_datasets/DATASET-GSE300311.json` | `00_raw/json/datasets/geo/GSE300311/geo_esummary.json`; `00_raw/txt/datasets/geo/GSE300311/series_soft.txt`; `00_raw/json/datasets/geo/GSE300311/source_record.json` |
| `DATASET-GSE298111` | `03_experimental_datasets/DATASET-GSE298111.json` | `00_raw/json/datasets/geo/GSE298111/geo_esummary.json`; `00_raw/txt/datasets/geo/GSE298111/series_soft.txt`; `00_raw/json/datasets/geo/GSE298111/source_record.json` |

The excluded GEO candidates are represented as curation decisions:

- `08_curation_decisions/CUR-EXCLUDE-GSE297057.json`
- `08_curation_decisions/CUR-EXCLUDE-GSE301973.json`

### GT-REQUIRE-SYNTHETIC-PROVENANCE

Purpose: verify that generated ELN/LIMS-style operational records are clearly
marked as synthetic and do not imply real patient data.

Expected result:

- `expected_decision`: `approve_with_required_labeling`
- `synthetic_records_must_include_provenance`: `synthetic_from_public_structure`

Source entities and Raw Layer files:

| Source entity | Normalized entity | Raw Layer files |
| --- | --- | --- |
| `SYN-LIMS-001` | `03_experimental_datasets/SYN-LIMS-001.json` | `00_raw/csv/synthetic_eln_lims/lims_sample_manifest.csv` |
| `SYN-LIMS-010` | `03_experimental_datasets/SYN-LIMS-010.json` | `00_raw/csv/synthetic_eln_lims/lims_sample_manifest.csv` |

Supporting synthetic operational raw files:

- `00_raw/txt/synthetic_eln_lims/eln_experiment_notebook.txt`
- `00_raw/txt/synthetic_eln_lims/lims_quality_control_report.txt`

### GT-ANSWER-GROUNDED-QUERY

Purpose: verify that a search/chat answer about the HLS knowledge-mining
baseline is grounded in citations and preserves Raw Layer traceability.

Expected result:

- `expected_decision`: `answer_with_citations`
- `minimum_citation_count`: `2`
- `must_include_raw_source_trace`: `true`

Source entities and Raw Layer files:

| Source entity | Normalized entity | Raw Layer files |
| --- | --- | --- |
| `RDOC-PMC6889286` | `01_research_documents/RDOC-PMC6889286.json` | `00_raw/xml/articles/pmc_oa/PMC6889286/article.xml`; `00_raw/json/articles/pmc_oa/PMC6889286/europe_pmc_metadata.json`; `00_raw/xml/articles/pmc_oa/PMC6889286/pmc_oa_license.xml` |
| `TRIAL-NCT02296125` | `02_clinical_trials/TRIAL-NCT02296125.json` | `00_raw/json/trials/clinicaltrials_gov/NCT02296125/study.json`; `00_raw/pdf/trials/clinicaltrials_gov/NCT02296125/Prot_000.pdf`; `00_raw/pdf/trials/clinicaltrials_gov/NCT02296125/SAP_001.pdf` |
| `DATASET-GSE323366` | `03_experimental_datasets/DATASET-GSE323366.json` | `00_raw/json/datasets/geo/GSE323366/geo_esummary.json`; `00_raw/txt/datasets/geo/GSE323366/series_soft.txt`; `00_raw/json/datasets/geo/GSE323366/source_record.json` |
| `LBL-TAGRISSO-OPENFDA` | `05_regulatory_submissions/LBL-TAGRISSO-OPENFDA.json` | `00_raw/json/regulatory/OPENFDA_LABEL_TAGRISSO/OPENFDA_LABEL_TAGRISSO.json` |

## Multi-format Agent Inputs

The Raw Layer also includes format replicas for extraction consistency tests.
Use:

```text
00_raw/agent_document_manifest.json
```

This manifest maps each `document_id` to equivalent files in:

```text
00_raw/txt/agent_inputs/<category>/<document_id>.txt
00_raw/md/agent_inputs/<category>/<document_id>.md
00_raw/html/agent_inputs/<category>/<document_id>.html
00_raw/pdf/agent_inputs/<category>/<document_id>.pdf
```

These replicas are useful when validating that different input formats produce
the same or similar extracted entities. They are not the source of truth; the
source of truth remains the public/synthetic Raw Layer files referenced by each
normalized entity's `raw_sources`.

## Quick Lookup Commands

List all cases:

```bash
find dataset-seed/09_decision_ground_truth -name 'GT-*.json' | sort
```

Show source entities and expected output for one case:

```bash
jq '{scenario_id, expected_agent, expected_decision, source_entities, expected_outputs}' \
  dataset-seed/09_decision_ground_truth/GT-ANSWER-GROUNDED-QUERY.json
```

Show the Raw Layer files for one normalized entity:

```bash
jq '.raw_sources' dataset-seed/02_clinical_trials/TRIAL-NCT02296125.json
```
