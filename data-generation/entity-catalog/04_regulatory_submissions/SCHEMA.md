# 04 Regulatory Submissions Schema

Regulatory application and product-label entities derived from openFDA and Drugs@FDA raw files.
Source documents (`REGDOC-*`: approval letters, reviews, EPAR) are not materialized — the demo
links the application + label only.

## Entity types

- `regulatory_application`
- `product_label`

## Required fields

- `document_id`
- `document_type`
- `source_system`
- `application_number` where applicable
- `raw_sources`
- `provenance`
