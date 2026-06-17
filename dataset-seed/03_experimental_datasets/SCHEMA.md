# 03 Experimental Datasets Schema

Experimental dataset and sample entities derived from GEO and synthetic LIMS raw files.

## Entity types

- `experimental_dataset`
- `assay_sample`
- `synthetic_lims_sample`

## Required fields

- dataset entities: `dataset_id`, `title`, `taxon`, `sample_count`, `sample_accessions`, `raw_sources`
- sample entities: `sample_id`, `dataset_id` or `source_geo_series`, `model`, `condition`, `raw_sources`
- all entities: `document_type`, `source_system`, `provenance`, `privacy_posture`
