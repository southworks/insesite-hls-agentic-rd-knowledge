# Case 2 — Human review needed

**User action:** Upload research package via ingestion console (controlled UI trigger).
**Ingest:** 5 OA JATS XML articles — see `ingest/` (`PMC*_article.xml`).
**Expected outcome:** Ingestion flags exclusions (`CUR-EXCLUDE-PMC4771182`, GEO datasets excluded); content indexed by metadata-linking; curator gate returns `denied_pending_human_review` (indexed content removal deferred to a future iteration).
**Legacy ID:** ING-002

### User Input

Upload and ingest: 5 OA research articles (PMC6889286, PMC5447962, PMC13070087, PMC13129538, PMC13143971) via ingestion console.
