# 04 Biomarkers and Targets Schema

Compound, target, and biomarker entities derived from ChEMBL plus curated raw-layer evidence.

## Entity types

- `compound`
- `molecular_target`
- `biomarker`

## Required fields

- `entity_id`
- `document_type`
- `preferred_name` or `name`
- `linked_compound_ids` where applicable
- `raw_sources`
- `provenance`
