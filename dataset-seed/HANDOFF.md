# Agent Handoff — the two HLS phases: what each step receives, produces, and passes on

This document is the precise handoff map for the *HLS – Agentic R&D knowledge mining* workflow.
It is the bridge between the architecture diagram (`../HLS - Agentic R&D knowledge mining.png`,
summarized in `../workflow-summary.md`) and `dataset-seed/`.

See [TEST_CASES.md](TEST_CASES.md), [TESTING_GUIDE.md](TESTING_GUIDE.md),
[RAW_LAYER.md](RAW_LAYER.md), and [SCENARIO_ORGANIZATION.md](SCENARIO_ORGANIZATION.md).

## The model: two sequential phases, one human actor each

HLS is **two sequential phases**, each closed by a **distinct human actor**:

1. **Phase 1 — Ingestion & structuring** (`ING-*`): `ingestion & translation → metadata & linking →
   [Knowledge curator approves] → persistence into the CMS/knowledge base`.
2. **Phase 2 — Search & compliance** (`QRY-*`): `search & chat → curation & compliance →
   [Compliance owner approves] → response`.

Phase 2 starts **after** phase 1's approval — **immediately** (same session) or **deferred** (a
researcher queries the hub days later, or a compliance audit is scheduled). The orchestrator
persists phase-1 context and resumes phase 2 without re-running ingestion. The headline demo shows
this statefully: query an **empty** KB (no answer) → ingest → query the **populated** KB (grounded answer).

Each phase's entry point is a **controlled UI action** (a button/process trigger), **not** a
free-form chatbot. In ingestion the trigger is **document upload**: manual file upload stands in for
the "Connect Portals" MCP, and the uploaded assets *represent* data that could come from external
medical portals (a **conceptual external connector**, not the literal interaction).

## What we materialize — and what we don't

> The demo **traverses every agent and both human actors**, but we generate datasets **only for the
> agents/actions that actually consume data** (the same principle the inesite dataset follows).

| Step | Phase | Kind | Materialized? |
|---|---|---|---|
| Ingestion & translation | 1 | data-consuming agent (upload) | **yes** — `01_ingestion_translation/` |
| Metadata & linking | 1 | data-consuming agent (RAG) | **yes** — `02_metadata_linking/` |
| Knowledge curator approval | 1 | human gate | no — memory stage in `scenario.json` |
| Persistence | 1 | sink | no — memory stage in `scenario.json` |
| Search & chat | 2 | data-consuming agent (RAG) | **yes** — `01_search_chat/` |
| Curation & compliance | 2 | data-consuming agent | **yes** — `02_curation_compliance/` |
| Compliance owner approval | 2 | human gate | no — memory stage in `scenario.json` |
| Response | 2 | output | **yes** — `03_response/` (output only) |

A **materialized agent stage** has `agent_input.json` + `input/` + `expected_output/`. The
**response** stage has only `response.json`. The **memory stages** (the two human approvals and
persistence) carry no folder — their `gate_record` / `persisted` payloads live in the scenario's
full `scenario.json` answer key, which the demo still validates against.

## Phase 1 — Ingestion & structuring (load knowledge)

| # | Step | Receives | Consumes (data) | Produces | Hands off to |
|---|---|---|---|---|---|
| 1 | **Ingestion & translation** | uploaded raw docs (`input/`) | the uploaded articles / synthetic ELN-LIMS | `01_research_documents` (+ `03_experimental_datasets`/`SYN-LIMS`), with license/PHI screening | Metadata & linking |
| 2 | **Metadata & linking** | the normalized docs from step 1 (`input/`) | **RAG-retrieves** the trial/regulatory entities to link against (from the KB / Vector DB — *not* staged) | `05_compounds_targets` (extraction) + `06_evidence_links` (links) | Knowledge curator |
| 3 | **Knowledge curator** (gate) | the **joint** output of steps 1–2 | — | approve / approve-with-labeling / needs-review / deny | Persistence |
| 4 | **Persistence** (sink) | the approved entity set | — | entities persisted into the CMS/KB | — (KB ready for phase 2) |

Metadata & linking is the RAG consumer of phase 1: its `input/` is the **upstream handoff** (the
normalized `RDOC-*` docs); the trial/regulatory entities it links them to are pulled via retrieval
(`agent_input.retrieved_via_rag`), so they are *referenced*, not copied into `input/`.

## Phase 2 — Search & compliance (query the knowledge)

| # | Step | Receives | Consumes (data) | Produces | Hands off to |
|---|---|---|---|---|---|
| 1 | **Search & chat** | the query (`prompt.txt`) + retrieval scope | the **persisted** KB entities (`input/`, empty if KB empty) | draft grounded answer + citations + raw-source trace (or "no grounded evidence") | Curation & compliance |
| 2 | **Curation & compliance** | the draft answer + its cited entities (`input/`) | the cited entities | review verdict: approve / flag / confirm-no-data | Compliance owner |
| 3 | **Compliance owner** (gate) | the **joint** output of steps 1–2 | — | approve / deny | Response |
| 4 | **Response** (output) | the approved answer | — | the response returned to the user | — |

Phase 2 runs **against already-persisted knowledge**. If nothing was ingested, search returns no
grounded answer — the first beat of the demo.

## Diagram support blocks → dataset

- **Retrieval Tool Components** (Cohere Embed → Vector DB → Cohere Rerank → Top-N): used by
  **metadata & linking** (phase 1) and **search & chat** (phase 2); fed by the canonical corpus and
  the multi-format `00_raw/_corpus/{txt,md,html,pdf}/agent_inputs/` evidence cards.
- **Data / systems of record**: Research articles → `01_research_documents`; Partner/vendor repos →
  `csv/partner_vendor_repositories/` (the conceptual "Connect Portals" connector); Preference/compliance
  → `08_policy_rag`. *Brand guidelines* and *Inventory* are generic template blocks — not HLS requirements.
- **Governance & resp. AI**: Evaluations → `09_decision_ground_truth`; Safety & compliance →
  `08_policy_rag` + `07_curation_decisions` + `privacy_posture`.

## Scenarios (the test cases)

Four ingestion paths (phase 1) and two search paths (phase 2). Defined in [`scenarios.py`](scenarios.py);
see [TEST_CASES.md](TEST_CASES.md):

| Scenario | Phase | Path | Final outcome | Where it lives |
| --- | --- | --- | --- | --- |
| `ING-001` | 1 | full approval (clean upload) | `approved_persisted` | `DEMO_SCENARIO/2-…` |
| `ING-002` | 1 | guardrail + curator review | `needs_human_review` | `00_raw/ING-002_…` |
| `ING-003` | 1 | synthetic provenance | `approved_with_required_labeling` | `00_raw/ING-003_…` |
| `ING-004` | 1 | sensitive content blocked | `denied_not_persisted` | `00_raw/ING-004_…` |
| `QRY-001` | 2 | empty KB | `no_grounded_answer` | `DEMO_SCENARIO/1-…` |
| `QRY-002` | 2 | populated KB | `answer_with_citations` | `DEMO_SCENARIO/3-…` |

**Stateful headline demo** (under `00_raw/DEMO_SCENARIO/`, numbered in run order): `1-QRY-001`
(search, no data) → `2-ING-001` (ingest) → `3-QRY-002` (search again, the grounded answer appears).
The guardrail variants `ING-002/003/004` are standalone at the `00_raw/` root.

## Start any materialized step in isolation

Each materialized stage folder is self-contained, so a demo can begin mid-phase "as if the previous
steps had run". Under `00_raw/.../<NN>_<stage>/`:

- `agent_input.json` — the structured payload that **starts** that agent (the handoff contract).
- `input/` — the documents the step starts from (uploaded raw for ingestion; the upstream-entity
  handoff downstream; `prompt.txt` for search). Empty for a query against an empty KB.
- `expected_output/` — the entities + `_expected_output.json` the agent **would** produce, so you can
  hand a guaranteed output to the next step without running the previous one.

```bash
# start at Search & chat against a populated KB
cat 00_raw/DEMO_SCENARIO/3-QRY-002_grounded/01_search_chat/agent_input.json
ls  00_raw/DEMO_SCENARIO/3-QRY-002_grounded/01_search_chat/input/
cat 00_raw/DEMO_SCENARIO/3-QRY-002_grounded/01_search_chat/expected_output/_expected_output.json
```

The full chain — including the human approvals and persistence the demo traverses but we don't
materialize — is the `stages[]` array in each scenario's `scenario.json` (mirror of
`09_decision_ground_truth/<ID>.json`).

## Notes vs the loan reference

- `loan-mortgage-agents` collapses its doc-processing agents into a **single underwriting decision**,
  so its `09` schema has **no conversational agent and no NL-query input**. HLS's search phase needs
  one, so the `query` + `expected_citations` / `expected_answer_points` are the extension beyond loan.
- Like the inesite dataset, only the **data-consuming** agents get raw-layer folders; the rest of the
  chain (human actors, persistence) lives in the `scenario.json` answer key.
- The normalized entity catalog is **trimmed to only what the demo uses** (noise reduction): no
  per-sample GEO entities, no biomarker catalog, no extra trials/links/regulatory source docs.
