# 05 Compounds and Targets Schema

Compound and molecular-target entities derived from ChEMBL — the entities the metadata & linking
agent extracts from the ingested evidence. Biomarker entities (`BMK-*`) are not materialized.

## Entity types

- `compound`
- `molecular_target`

## Required fields

- `entity_id`
- `document_type`
- `preferred_name`
- `linked_compounds` / `target_chembl_id` where applicable
- `raw_sources`
- `provenance`
