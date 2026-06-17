# 05 Regulatory Submissions Schema

Regulatory application, label, and source-document entities derived from openFDA, Drugs@FDA, and EMA raw files.

## Entity types

- `regulatory_application`
- `product_label`
- `regulatory_source_document`

## Required fields

- `document_id`
- `document_type`
- `source_system`
- `application_number` where applicable
- `compound_id` or product identifiers where applicable
- `raw_sources`
- `provenance`
