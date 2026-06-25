# 07 Curation Decisions Schema

Deny/exclude decisions for source inclusion and compliance guardrails (the guardrail evidence the
ING-002/ING-004 scenarios reference). Per-source "approve" decisions are implicit.

## Required fields

- `decision_id`
- `document_type` = `curation_decision`
- `source_id`
- `source_type`
- `decision`
- `reason`
- `required_human_review`
- `policy_refs`
- `curator`
- `decision_date`
- `raw_sources`
- `provenance`
