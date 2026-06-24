# 09 Decision Ground Truth Schema

End-to-end ground truth for HLS's **two isolated flows** — one rollup per scenario:

- `ING-XXX.json` — INGESTION: upload -> ingestion/translation -> metadata linking ->
  human approval -> persistence into the CMS/knowledge base.
- `QRY-XXX.json` — SEARCH: UI query -> search/chat retrieval -> curation/compliance review
  of the result -> response. Runs later, against already-persisted knowledge.

The flows are decoupled in time (no single orchestrator over all agents). Defined once in
`scenarios.py`; built into `00_raw/{ING,QRY}-XXX_<path>/` by `build_scenario_folders.py`.

## Scenario-level fields

- `scenario_id` (e.g. `ING-001`, `QRY-001`)
- `document_type` = `decision_ground_truth`
- `scenario_kind` = `e2e_flow_path`
- `flow` = `ingestion` | `search`
- `title`, `path` (e.g. `full_approval`, `guardrail_review`, `synthetic_provenance`,
  `sensitive_blocked`, `no_data`, `grounded`)
- `scenario_folder` (the `00_raw/` folder)
- `trigger` — the controlled UI action that starts the flow (button/process, not a chatbot)
- `kb_state` (search only) = `empty` | `populated`
- `stages` (ordered steps, see below)
- `final_outcome`, `required_human_review`, `raw_sources`

## Per-stage fields (`stages[]`)

- `order`, `stage`, `kind` (`trigger` | `agent` | `gate` | `sink` | `output`), `agent` (nullable)
- `raw_layer_folder` — the self-contained `00_raw/.../<stage>/` folder
- primary payload, keyed by kind: `trigger` | `agent_input` | `gate_record` | `persisted` |
  `response`. For an `agent` stage, `agent_input` is the structured payload to START that agent
  in isolation "as if the upstream stages had run" (the handoff contract); for `search_chat`
  it carries the NL `query` + `retrieval_scope_entities`.
- `input_entities` — entities handed in from upstream (copied to `<stage>/input/`)
- `output_entities` — entities this stage would produce (copied to `<stage>/expected_output/`)
- `expected_output` — measurable expectations (counts, links, answer points/citations, decision)
- `decision`, `gate` (`approved` | `denied` | `denied_pending_human_review` |
  `approved_with_labeling` | `null`)
