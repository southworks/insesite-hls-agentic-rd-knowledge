# Case 9 — Multi-turn grounded session

**User action:** Run two queries in the same session against a populated KB (after ING-001), then Curate.
**Prompts (in order):**
1. `prompts/01-resistance-mechanisms.txt` — resistance mechanisms
2. `prompts/02-flaura-context.txt` — FLAURA / first-line trial context

**Expected outcome:** Both turns grounded with citations; Curate reviews the full `chatResponses` set; Compliance Reviewer approves.
**Legacy ID:** QRY-005

**Agent capabilities tested:** search-chat multi-turn accumulation; curation-compliance session-wide review.
