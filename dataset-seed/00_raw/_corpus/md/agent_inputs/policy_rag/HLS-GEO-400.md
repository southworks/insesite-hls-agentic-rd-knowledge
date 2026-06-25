# Cell-line dataset preference

Policy RAG evidence card

- Document ID: `HLS-GEO-400`
- Category: `policy_rag`
- Source entity: `08_policy_rag/HLS-GEO-400.json`

## Policy Rule

| Field | Value |
| --- | --- |
| Policy ID | HLS-GEO-400 |
| Policy ref | HLS-GEO-400 |
| Title | Cell-line dataset preference |
| Rule | Use GEO records whose samples are cell-line or assay-level records for the first dataset iteration. |
| Threshold | Reject records whose sample titles reference patient FFPE, before/after treatment patient specimens, or similar patient-derived labels. |
| Action | allow_cell_line_dataset_reject_patient_sample_dataset |
| Source refs | NCBI GEO; source_catalog_exclusions |

## Provenance

| Field | Value |
| --- | --- |
| Provenance | curated_from_raw_layer_policy_sources |
| Privacy posture |  |
| Raw source trace | 00_raw/_corpus/raw_manifest.json; _source/source_catalog.json |
