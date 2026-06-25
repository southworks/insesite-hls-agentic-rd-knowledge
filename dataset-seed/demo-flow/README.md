# Demo flow — stateful headline demo

Run these steps **in order** to show the full HLS narrative: query empty KB → ingest → query again and get grounded answer.

| Step | Folder | Legacy ID | Action |
|------|--------|-----------|--------|
| 1 | `step-01-no-data/` | QRY-001 | Query empty KB |
| 2 | `step-02-full-approval/` | ING-001 | Upload & ingest clean OA articles |
| 3 | `step-03-grounded-query/` | QRY-002 | Same query — grounded answer with citations |

**Prerequisite for step 3:** step 2 must complete successfully (KB populated).
