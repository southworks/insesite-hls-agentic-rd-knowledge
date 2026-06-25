# Open-access full-text license check

Policy RAG evidence card

- Document ID: `HLS-LIC-200`
- Category: `policy_rag`
- Source entity: `08_policy_rag/HLS-LIC-200.json`

## Policy Rule

| Field | Value |
| --- | --- |
| Policy ID | HLS-LIC-200 |
| Policy ref | HLS-LIC-200 |
| Title | Open-access full-text license check |
| Rule | Store article full text only when PMC OA license metadata confirms acceptable reuse terms. |
| Threshold | PMC OA API license must match the catalog expected_license before article.xml is kept. |
| Action | allow_full_text_when_license_verified |
| Source refs | PMC Open Access; Europe PMC |

## Provenance

| Field | Value |
| --- | --- |
| Provenance | curated_from_raw_layer_policy_sources |
| Privacy posture |  |
| Raw source trace | 00_raw/_corpus/raw_manifest.json; 00_raw/_corpus/raw_manifest.json |
