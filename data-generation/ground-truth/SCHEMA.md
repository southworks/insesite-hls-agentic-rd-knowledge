# 09 Decision Ground Truth Schema

End-to-end ground truth for HLS's **two sequential phases** — one rollup per scenario, the full
answer key the demo validates against:

- `ING-XXX.json` — PHASE 1 (ingestion & structuring): ingestion/translation -> metadata linking
  -> [knowledge curator approves] -> persistence into the CMS/knowledge base.
- `QRY-XXX.json` — PHASE 2 (search & compliance): search/chat -> curation/compliance ->
  [compliance owner approves] -> response. Runs immediately after phase 1 or deferred.

The demo traverses every agent and both human actors, but datasets are materialized only for the
data-consuming agents (+ the response output). Defined once in `scenarios.py`; built into
`00_raw/DEMO_SCENARIO/<n>-<ID>_<path>/` and `00_raw/<ID>_<path>/` by `build_scenario_folders.py`.

## Scenario-level fields

- `scenario_id` (e.g. `ING-001`, `QRY-001`)
- `document_type` = `decision_ground_truth`
- `scenario_kind` = `e2e_phase_path`
- `flow` = `ingestion` | `search`,  `phase` = `1` | `2`
- `title`, `path` (e.g. `full_approval`, `guardrail_review`, `synthetic_provenance`,
  `sensitive_blocked`, `no_data`, `grounded`)
- `scenario_folder` (the `00_raw/...` base folder)
- `trigger` — the controlled UI action that starts the phase (button/process, not a chatbot)
- `kb_state` (search only) = `empty` | `populated`
- `stages` (every ordered step, see below)
- `final_outcome`, `required_human_review`, `raw_sources`

## Per-stage fields (`stages[]`)

- `order`, `stage`, `kind` (`agent` | `output` | `gate` | `sink`), `agent` / `actor` (nullable)
- `materialized` — `true` for data-consuming agents + the response output (they get a folder);
  `false` for the human-approval gates and persistence (memory only — no folder)
- `raw_layer_folder` — the self-contained `00_raw/.../<NN>_<stage>/` folder (materialized stages only)
- primary payload, keyed by kind: `agent_input` (agent) | `response` (output) |
  `gate_record` (gate) | `persisted` (sink). For an `agent` stage, `agent_input` is the structured
  payload to START that agent in isolation "as if the upstream stages had run" (the handoff
  contract); for `search_chat` it carries the NL `query` + `retrieval_scope_entities`.
- `input_entities` — entities handed in from upstream (copied to `<stage>/input/`)
- `output_entities` — entities this stage would produce (copied to `<stage>/expected_output/`)
- `expected_output` — measurable expectations (counts, links, answer points/citations, decision)
- `decision`, `gate` (`approved` | `denied` | `denied_pending_human_review` |
  `approved_with_labeling` | `null`)
