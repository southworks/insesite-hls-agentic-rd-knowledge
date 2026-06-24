# Agent Handoff — the two HLS flows: what each step receives, produces, and passes on

This document is the precise handoff map for the *HLS – Agentic R&D knowledge mining* workflow.
It is the bridge between the proposal diagram and `dataset-seed/`.

See [TEST_CASES.md](TEST_CASES.md), [TESTING_GUIDE.md](TESTING_GUIDE.md),
[RAW_LAYER.md](RAW_LAYER.md), and [SCENARIO_ORGANIZATION.md](SCENARIO_ORGANIZATION.md).

## Interpretation correction

An earlier version of this dataset modeled HLS as **one continuous 4-agent flow with a single
orchestrator** (`Orchestrator → Ingestion → Metadata/Linking → Search/Chat → Curation`). The team
realigned: **that was wrong.** HLS is **two separate processes, decoupled in time** — there is no
single orchestrator spanning all four agents:

1. **Ingestion flow** — *load* knowledge. Runs earlier; ends by persisting into the CMS/knowledge base.
2. **Search flow** — *query* that knowledge. Runs later, against what was already persisted.

There is **no runtime coupling** between them: you ingest first, you search afterwards.

## System objective

A compliance-safe agentic R&D knowledge hub. The **ingestion flow** turns heterogeneous research
material into an auditable, linked knowledge base; the **search flow** answers grounded questions
against it with citations and a raw-source trace. The whole dataset is anchored on
`osimertinib / Tagrisso / AZD9291` in EGFR-mutated NSCLC so both flows operate over one coherent corpus.

Each flow's entry point is a **controlled UI action** (a button/process trigger), **not** a
free-form chatbot — chatbot-style entry would reduce determinism in the demo. In ingestion the
trigger is **document upload**: manual file upload **replaces** the "Connect Portals" MCP for the
demo, and the uploaded assets *represent* data that could come from external medical portals
("Connect Portals" stays a **conceptual external connector**, not the literal interaction).

## Flow 1 — Ingestion (load knowledge)

`upload → ingestion/translation → metadata linking → human approval → persistence (CMS/KB)`

| # | Step | Kind | Receives | Produces | Hands off to |
|---|---|---|---|---|---|
| 1 | **Upload** | trigger (UI) | uploaded files (+ conceptual `portal_source_manifest`) | the raw docs to ingest | Ingestion |
| 2 | **Ingestion & translation** | agent | uploaded raw docs | `01_research_documents`, `03_experimental_datasets` (+ `SYN-LIMS`), … with license/PHI screening (accept/deny/exclude) | Metadata & linking |
| 3 | **Metadata & linking** | agent | normalized entities | `04_biomarkers_and_targets` (extraction), `07_evidence_links` (links) | Human approval |
| 4 | **Human approval** | gate (HITL) | the ingested+linked batch | approve / approve-with-labeling / needs-review / deny | Persistence |
| 5 | **Persistence** | sink | the approved entity set | entities persisted into the CMS/knowledge base (`01_*..08_*`) | — (KB ready for search) |

Compliance during ingestion lives inside steps 2–3 (license/PHI screening → `08_curation_decisions`
exclusions) and the **human approval** gate. There is no separate curation *agent* in this flow.

## Flow 2 — Search (query the knowledge)

`UI query → search/chat retrieval → curation/compliance review → response`

| # | Step | Kind | Receives | Produces | Hands off to |
|---|---|---|---|---|---|
| 1 | **Query** | trigger (UI) | a controlled preset question + `kb_state` | the query to run | Search & chat |
| 2 | **Search & chat** | agent | `query` + retrieval scope over the **persisted** KB | draft grounded answer + citations + raw-source trace (or "no grounded evidence" if the KB is empty) | Curation & compliance |
| 3 | **Curation & compliance** | agent | the draft answer | review verdict: approve / flag / confirm-no-data (checks grounding + sensitive content) | Response |
| 4 | **Response** | output | the reviewed answer | the response returned to the user | — |

The search flow runs **against already-persisted knowledge**. If nothing was ingested yet, step 2
returns no grounded answer — which is exactly the first beat of the demo.

## Diagram support blocks → dataset

- **Retrieval Tool Components** (Cohere Embed → Vector DB → Cohere Rerank → Top-N): fed by the
  multi-format `00_raw/_corpus/{txt,md,html,pdf}/agent_inputs/` evidence cards (used in the search flow).
- **Data / systems of record**: Research articles → `01`; Partner/vendor repos →
  `csv/partner_vendor_repositories/` (the conceptual "Connect Portals" connector); Preference/compliance
  → `06_policy_rag`. *Brand guidelines* and *Inventory* are generic template blocks — not HLS requirements.
- **Governance & resp. AI**: Evaluations → `09_decision_ground_truth`; Safety & compliance →
  `06_policy_rag` + `08_curation_decisions` + `privacy_posture`.

## Scenarios (the test cases)

Four ingestion paths and two search paths (differing at the gates / KB state). Defined in
[`scenarios.py`](scenarios.py); see [TEST_CASES.md](TEST_CASES.md):

| Scenario | Flow | Path | Final outcome |
| --- | --- | --- | --- |
| `ING-001` | ingestion | full approval (clean upload) | `approved_persisted` |
| `ING-002` | ingestion | guardrail + human review | `needs_human_review` |
| `ING-003` | ingestion | synthetic provenance | `approved_with_required_labeling` |
| `ING-004` | ingestion | sensitive content blocked | `denied_not_persisted` |
| `QRY-001` | search | empty KB | `no_grounded_answer` |
| `QRY-002` | search | populated KB | `answer_with_citations` |

**Stateful demo:** run `QRY-001` (search, no data) → `ING-001` (ingest) → `QRY-002` (search again,
the grounded answer now appears).

## Start any step in isolation

Each scenario's stage folder is self-contained, so a demo can begin mid-flow "as if the previous
steps had run". Under `00_raw/<ID>_<path>/<stage>/`:

- the stage's **primary file** (`trigger.json` | `agent_input.json` | `gate.json` | `persisted.json`
  | `response.json`) — the payload that **starts** that step.
- `input/` — the documents the step starts from (raw for ingestion; upstream entities downstream).
  *agent* steps always have one (empty for a query against an empty KB).
- `expected_output/` — for *agent* steps: the entities + `_expected_output.json` they **would**
  produce, so you can hand a guaranteed output to the next step without running the previous one.

```bash
# start at Search & chat against a populated KB
cat 00_raw/QRY-002_grounded/02_search_chat/agent_input.json
ls  00_raw/QRY-002_grounded/02_search_chat/input/
cat 00_raw/QRY-002_grounded/02_search_chat/expected_output/_expected_output.json
```

## Notes vs the loan reference

- `loan-mortgage-agents` collapses its doc-processing agents into a **single underwriting decision**,
  so its `09` schema has **no conversational agent and no NL-query input**. HLS's search flow needs
  one, so the `query` + `expected_citations` / `expected_answer_points` are the extension beyond loan.
- Like loan (`00_raw/<bucket>/APP-XXX/`), scenarios use a trackable prefix and the path in the folder
  name (`00_raw/ING-XXX_<path>/`, `00_raw/QRY-XXX_<path>/`). HLS adds a per-stage sub-structure
  because each flow is a multi-step chain.
- Deliberately **not** modeled: a single orchestrator over both flows (the realignment removed it),
  document de-duplication/version pairs, and multilingual translation artifacts.
