# Testing Guide

Run demos using files under `dataset-seed/cases/`. No per-stage folder tree is required.

## Quick start

1. Load `dataset-seed/policies/hls_policies.txt` into your RAG pipeline.
2. Pick a case from [`dataset-seed/README.md`](../../dataset-seed/README.md).
3. Upload flat files from `<case>/ingest/` when the case requires ingest.
4. For the headline demo (`case-04-demo`), run steps 1→3 in order using `prompts/` and `ingest/`.

## Headline demo (case-04-demo)

| Step | Legacy ID | Action | Files |
|------|-----------|--------|-------|
| 1 | QRY-001 | Query empty KB | `prompts/01-no-data-prompt.txt` |
| 2 | ING-001 | Upload 5 OA articles | `ingest/PMC*_article.xml` |
| 3 | QRY-002 | Same query, grounded answer | `prompts/03-grounded-query-prompt.txt` |

Expected outcomes are documented in each case README and in `ground-truth/<ID>.json`.

## Standalone stress cases

| Case folder | Legacy ID | Ingest |
|-------------|-----------|--------|
| `case-01-human-review` | ING-002 | 5 × `PMC*_article.xml` |
| `case-02-approval-labeling` | ING-003 | ELN/LIMS csv + txt |
| `case-03-sensitive-denied` | ING-004 | `CUR-EXCLUDE-GSE301973.txt` |

## Rebuild test data

```bash
cd data-generation/scripts
python3 generate_raw_layer.py
python3 build_case_folders.py
```

Optional: `python3 generate_agent_documents.py` before `build_case_folders.py` if ING-004 agent-input txt must be refreshed.
