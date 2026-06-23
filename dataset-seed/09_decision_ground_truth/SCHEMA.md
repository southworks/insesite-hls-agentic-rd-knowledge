# 09 Decision Ground Truth Schema

End-to-end ground truth: one rollup per scenario (`RKM-XXX.json`). Each scenario is a full
path through the workflow (Orchestrator -> Ingestion -> Metadata/Linking -> Search/Chat ->
Curation/Compliance); the four scenarios differ at the human-in-the-loop gates. Defined once
in `scenarios.py`; built into `00_raw/RKM-XXX_<path>/` by `build_scenario_folders.py`.

## Scenario-level fields

- `scenario_id` (e.g. `RKM-001`)
- `document_type` = `decision_ground_truth`
- `scenario_kind` = `e2e_workflow_path`
- `title`, `path` (e.g. `full_approval`, `guardrail_review`, `synthetic_provenance`, `curation_denied`)
- `scenario_folder` (the `00_raw/` folder), `orchestrator_request`
- `stages` (ordered per-agent steps, see below)
- `final_outcome`, `required_human_review`, `raw_sources`

## Per-stage fields (`stages[]`)

- `order`, `stage`, `agent`
- `raw_layer_folder` — the self-contained `00_raw/.../<stage>/` folder
- `agent_input` — structured payload to START this agent in isolation, "as if the upstream
  agents had already run" (the handoff contract). For `search_chat` this carries the NL `query`
  + `retrieval_scope_entities` — the one input the loan reference schema does not model.
- `input_entities` — normalized entities handed in from upstream (copied to `<stage>/input/`)
- `output_entities` — normalized entities this stage would produce (copied to `<stage>/expected_output/`)
- `expected_output` — measurable expectations (counts, required links, answer points/citations, decision)
- `decision`, `gate` (`approved` | `denied` | `denied_pending_human_review` | `approved_with_labeling` | `null`)
