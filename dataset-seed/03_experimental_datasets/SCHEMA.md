# 03 Experimental Datasets Schema

Experimental dataset entities derived from GEO, plus the synthetic LIMS samples used by the
synthetic-provenance scenario. Per-sample GEO entities (`SAMPLE-GSM*`) are intentionally not
materialized — the demo links at the dataset (series) level.

## Entity types

- `experimental_dataset`
- `synthetic_lims_sample`

## Required fields

- dataset entities: `dataset_id`, `title`, `taxon`, `sample_count`, `sample_accessions`, `raw_sources`
- synthetic sample entities: `sample_id`, `source_geo_series`, `model`, `condition`, `raw_sources`
- all entities: `document_type`, `source_system`, `provenance`, `privacy_posture`
