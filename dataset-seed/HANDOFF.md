# Agent Handoff — what each agent receives, produces, and passes on

This document is the precise **pasa-manos** (handoff) map between the agents in the
*HLS – Agentic R&D knowledge mining* workflow and the dataset entities that carry the
data across the chain. It is the bridge between the proposal diagram and `dataset-seed/`.

It is validated against the proposal workflow and the `loan-mortgage-agents` reference
convention. See [TEST_CASES.md](TEST_CASES.md), [RAW_LAYER.md](RAW_LAYER.md), and
[SCENARIO_ORGANIZATION.md](SCENARIO_ORGANIZATION.md).

## System objective

An agentic R&D knowledge hub that **ingests** heterogeneous research material,
**normalizes and links** it into an auditable evidence graph, makes it **searchable with
grounded citations**, and **curates/governs** it for compliance — every output traceable
back to a Raw Layer source. The whole dataset is anchored on
`osimertinib / Tagrisso / AZD9291` in EGFR-mutated NSCLC so the four capabilities operate
over one coherent corpus. The **Orchestrator – Research knowledge hub agent** routes a
request to the sub-agents; each ground-truth case is a per-agent entry point.

## Handoff chain

| # | Agent | Receives (`agent_input`) | Consumes (entities) | Produces (entities) | Hands off to | Validated by |
|---|---|---|---|---|---|---|
| 1 | **Ingestion & translation** | candidate raw docs + `portal_source_manifest` (connect portals) | `00_raw/_corpus/**` | `01_research_documents`, `02_clinical_trials`, `03_experimental_datasets` (+ `SYN-LIMS`), `05_regulatory_submissions` | Metadata & linking | `GT-INGEST-ARTICLES` |
| 2 | **Metadata & linking** | normalized entity ids (`normalized_entities` / `candidate_dataset_ids`) | `01–05` + `06_policy_rag` | `04_biomarkers_and_targets` (entity extraction), `07_evidence_links` (links) | HITL gate → Search / Curation | `GT-LINK-TRIAL-REGULATORY`, `GT-USE-CELL-LINE-DATASETS` |
| 3 | **Search & chat** | **`query`** (NL question) + `retrieval_scope_entities` | whole normalized corpus via Retrieval Components | grounded answer + citations + raw-source trace (not persisted as an entity) | user | `GT-ANSWER-GROUNDED-QUERY` |
| 4 | **Curation & compliance** | `records_under_review` + `enforced_policy_refs` + `raw_records` | `06_policy_rag`, candidate entities + raw | `08_curation_decisions` (allow/deny/review + flags) | HITL gate (Approved/Denied) | `GT-REQUIRE-SYNTHETIC-PROVENANCE` |

**Human-in-the-loop:** the two email approval gates in the diagram (after Metadata&linking
and after Curation&compliance) are carried by each stage's `gate` field
(`approved` | `denied` | `denied_pending_human_review` | `approved_with_labeling`) and the
scenario-level `required_human_review`.

## End-to-end scenarios (the test cases)

The handoff is exercised by four **e2e scenarios** (one full pass through all agents each),
which differ at the gates. Defined in [`scenarios.py`](scenarios.py), see [TEST_CASES.md](TEST_CASES.md):

| Scenario | Path | Final outcome |
| --- | --- | --- |
| `RKM-001` | full approval (happy path) | `approved` |
| `RKM-002` | guardrail + human review | `needs_human_review` |
| `RKM-003` | synthetic provenance | `approved_with_required_labeling` |
| `RKM-004` | denied at curation | `denied` |

## Diagram support blocks → dataset

- **Retrieval Tool Components** (Cohere Embed → Vector DB → Cohere Rerank → Top-N): fed by the
  multi-format `00_raw/_corpus/{txt,md,html,pdf}/agent_inputs/` evidence cards.
- **Data / systems of record**: Research articles → `01`; Partner/vendor repos →
  `csv/partner_vendor_repositories/` (now referenced by case 1's `portal_source_manifest`);
  Preference/compliance → `06_policy_rag`. *Brand guidelines* and *Inventory* are generic
  template blocks (Inventory belongs to the retail workflow) — not HLS requirements.
- **Governance & resp. AI**: Evaluations → `09_decision_ground_truth` (this IS the eval
  harness); Safety & compliance → `06_policy_rag` + `08_curation_decisions` + `privacy_posture`.

## Start the demo from any agent

Each scenario's stage folder is self-contained, so a demo can begin mid-chain "as if the
previous agents had run". Under `00_raw/RKM-XXX_<path>/<stage>/`:

- `agent_input.json` — the structured payload to **start** that agent in isolation.
- `input/` — the documents it starts from (raw for ingestion; upstream entities downstream).
- `expected_output/` — the entities + `_expected_output.json` it **would** produce, so you can
  hand a guaranteed output to the next stage without running the previous one.

```bash
# start at Search & chat in the happy path
cat 00_raw/RKM-001_full_approval/04_search_chat/agent_input.json
ls  00_raw/RKM-001_full_approval/04_search_chat/input/
cat 00_raw/RKM-001_full_approval/04_search_chat/expected_output/_expected_output.json
```

## Notes vs the loan reference

- `loan-mortgage-agents` collapses its doc-processing agents into a **single underwriting
  decision**, so its `09` schema has **no conversational agent and no NL-query input**. HLS
  needs one, so `agent_input.query` + `expected_answer_points` / `expected_citations` are the
  one extension beyond the loan schema. Everything else stays format-aligned with loan.
- Like loan (`00_raw/<bucket>/APP-XXX/`), scenarios use a trackable prefix and the path/outcome
  in the folder name (`00_raw/RKM-XXX_<path>/`). HLS adds a per-stage sub-structure because its
  workflow is a 4-agent chain, not a single decision.
- The orchestrator is modeled lightly as `01_orchestrator/request.json` per scenario (the routing
  request); deliberately **not** modeled: document de-duplication/version pairs and multilingual
  translation artifacts (no workflow/GT need, absent in the loan reference).
