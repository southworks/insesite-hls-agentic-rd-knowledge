# HLS Agentic R&D Knowledge Mining

Dataset and solution accelerator workspace for an HLS agentic R&D knowledge mining scenario with Cohere models on Azure.

## Scenario

This repository will define a compliance-safe dataset for an agentic research knowledge hub. The future dataset will start from a raw layer of public or simulated R&D knowledge artifacts and produce simulated downstream entities that represent how agents ingest, normalize, link, retrieve, curate, and govern research content.

The scenario is aligned to:

- Ingestion and translation of R&D source material
- Metadata extraction, entity extraction, and version linking
- Retrieval with Cohere Embed and Cohere Rerank
- Search and chat over grounded research evidence
- Curation, compliance review, and human approval decisions

## Dataset Direction

The raw layer is expected to represent artifacts such as research articles, protocols, ELN/LIMS-style records, datasets, results, submissions, partner or vendor repositories, and regional policy references.

The data source will be selected in a later step after reviewing public candidates that are suitable for healthcare and life sciences R&D knowledge mining, avoid patient-identifiable information, and do not introduce compliance concerns. If no public source fits the scenario cleanly, the raw layer may be generated synthetically using public material only as structural reference.

## Repository Scope

This initial commit intentionally includes only:

- `README.md`
- `.gitignore`

No datasets, schemas, generated files, source documents, or simulated outputs have been created yet.

## Alignment

This repository is intended to follow the same scenario-driven dataset approach used by:

- `southworks/loan-mortgage-agents`
- `southworks/inesite-agentic-inventory-planning`

Future commits should keep the raw layer and derived entities explicit, auditable, and easy to trace back to the scenario narrative.
