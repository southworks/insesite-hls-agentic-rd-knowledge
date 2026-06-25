# Case 2 — Human review needed

**User action:** Upload research package via ingestion console (controlled UI trigger).
**Ingest:** 5 OA articles — see `ingest/` (xml + json per PMC ID).
**Expected outcome:** Ingestion flags exclusions (`CUR-EXCLUDE-PMC4771182`, GEO datasets excluded); curator gate returns `denied_pending_human_review`; nothing persisted.
**Legacy ID:** ING-002
