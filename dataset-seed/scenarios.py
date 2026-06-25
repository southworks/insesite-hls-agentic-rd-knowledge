#!/usr/bin/env python3
"""
Scenario definitions for the HLS Agentic R&D knowledge mining dataset.

Single source of truth for the test cases. The workflow (see ../workflow-summary.md and the
architecture diagram) is **two sequential phases**, each closed by a distinct human actor:

  PHASE 1 — Ingestion & structuring:  ingestion & translation -> metadata & linking
                                      -> [Knowledge curator approves] -> persistence to CMS/KB
  PHASE 2 — Search & compliance:      search & chat -> curation & compliance
                                      -> [Compliance owner approves] -> response

Phase 2 can run immediately after phase 1's approval or be deferred (a researcher queries the
hub days later, or a compliance audit is scheduled). The orchestrator persists phase-1 context
and resumes phase 2 without re-running ingestion. The headline demo shows this statefully:
query an EMPTY KB (no answer) -> ingest -> query the POPULATED KB (grounded answer).

## What we materialize (and what we don't)

The demo *traverses every agent and both human actors*, but we only generate datasets for the
agents/actions that actually **consume data** — exactly the inesite pattern:

  - Data-consuming agents (RAG / upload):  ingestion_translation, metadata_linking,
    search_chat, curation_compliance  -> materialized as 00_raw/.../<NN>_<stage>/ folders with
    `agent_input.json` + `input/` + `expected_output/`.
  - The `response` output is materialized as an output-only folder (`response.json`).
  - The human-approval gates and persistence are **memory stages**: they appear in the full
    `scenario.json` answer key (`stages[]`) but get NO raw-layer folder — the orchestrator
    carries them in workflow memory.

The normalized entity catalog at the `dataset-seed/` root (01_research_documents ..
09_decision_ground_truth) is the single source of truth for the entities; build_scenario_folders.py
copies the relevant ones into each stage's `input/` and `expected_output/`.

Both generators import this module so the 09 rollups and the per-scenario Raw-Layer folders
stay aligned:

  - generate_normalized_layers.py  -> 09_decision_ground_truth/{ING,QRY}-XXX.json
  - build_scenario_folders.py      -> 00_raw/DEMO_SCENARIO/<n>-<ID>_<path>/ + 00_raw/<ID>_<path>/

Trackable prefixes: ING-### (ingestion phase) and QRY-### (query/search phase), like APP-XXX in loan.

## Stage shapes

  - kind == "agent"   : a data-consuming agent. Materialized. Fields: agent, agent_input,
                        input_entities | input_raw | input_entities_raw | input_from_output_raw,
                        output_entities, expected_output, decision, gate.
  - kind == "output"  : the final response. Materialized (output-only). Field: response.
  - kind == "gate"    : a human-in-the-loop approval. Memory only. Fields: actor, gate_record,
                        decision, gate.
  - kind == "sink"    : persistence into the CMS/KB. Memory only. Field: persisted.
"""

from __future__ import annotations

# Stage kinds that get a materialized 00_raw/.../<NN>_<stage>/ folder.
MATERIALIZED_KINDS = {"agent", "output"}

# kind -> (primary filename, payload key on the stage dict). Memory kinds (gate/sink) keep their
# payload in scenario.json only.
STAGE_PRIMARY = {
    "agent": ("agent_input.json", "agent_input"),
    "output": ("response.json", "response"),
    "gate": (None, "gate_record"),
    "sink": (None, "persisted"),
}

# The DEMO_SCENARIO bundle (stateful headline demo) and the standalone guardrail variants.
DEMO_SEQUENCE = ["QRY-001", "ING-001", "QRY-002"]   # numbered 1-, 2-, 3- under 00_raw/DEMO_SCENARIO/
STANDALONE_SCENARIOS = ["ING-002", "ING-003", "ING-004"]  # at 00_raw/<ID>_<path>/

PORTAL_MANIFEST = "00_raw/_corpus/csv/partner_vendor_repositories/partner_vendor_repository_index.csv"
SYNTHETIC_RAW = [
    "00_raw/_corpus/csv/synthetic_eln_lims/lims_sample_manifest.csv",
    "00_raw/_corpus/txt/synthetic_eln_lims/eln_experiment_notebook.txt",
    "00_raw/_corpus/txt/synthetic_eln_lims/lims_quality_control_report.txt",
]

# Controlled UI triggers (button/process), NOT free-form chatbots.
UI_UPLOAD = {
    "type": "ui_action",
    "action": "upload_and_ingest",
    "surface": "ingestion_console",
    "note": "controlled button/process trigger — not a free-form chatbot",
    "external_connector_conceptual": "Connect Portals — uploaded files stand in for data that "
                                     "could come from external medical portals",
}
UI_QUERY = {
    "type": "ui_action",
    "action": "run_query",
    "surface": "search_console",
    "note": "controlled preset query — not a free-form chatbot",
}

CURATOR = "Marisol Vega, Knowledge Curator (fictional)"
COMPLIANCE_OWNER = "Daniel Okafor, Compliance Owner (fictional)"

_GROUNDED_QUERY = (
    "What are the known mechanisms of resistance to osimertinib in EGFR-mutated NSCLC, "
    "and which clinical-trial and regulatory evidence support its first-line use?"
)
_ANSWER_POINTS = [
    "osimertinib is a third-generation EGFR-TKI active against the EGFR T790M resistance mutation",
    "acquired resistance is heterogeneous: MET/HER2 amplification, RAS-MAPK or RAS-PI3K pathway "
    "activation, novel fusion events, and histological/phenotypic transformation",
    "FLAURA (NCT02296125) is the first-line trial context for osimertinib",
    "the Tagrisso openFDA label is the regulatory anchor for the regimen",
]
_GROUNDED_CITATIONS = ["RDOC-PMC6889286", "TRIAL-NCT02296125", "LBL-TAGRISSO-OPENFDA"]
_RETRIEVAL_SCOPE = ["RDOC-PMC6889286", "TRIAL-NCT02296125", "DATASET-GSE323366", "LBL-TAGRISSO-OPENFDA"]

ACCEPTED_ARTICLES = ["RDOC-PMC6889286", "RDOC-PMC5447962", "RDOC-PMC13070087", "RDOC-PMC13129538", "RDOC-PMC13143971"]
ACCEPTED_DATASETS = ["DATASET-GSE323366", "DATASET-GSE323365", "DATASET-GSE272182", "DATASET-GSE300311", "DATASET-GSE298111"]
# Entities the metadata & linking agent RAG-retrieves to link the ingested articles against.
# They live in the KB/Vector DB (root catalog), so they are referenced — not staged in input/.
_LINK_TARGETS = ["TRIAL-NCT02296125", "TRIAL-NCT02151981", "REG-NDA208065", "LBL-TAGRISSO-OPENFDA"]
_LINKS = ["LINK-FLAURA-REG-NDA208065", "LINK-REG-NDA208065-LBL-TAGRISSO-OPENFDA"]
_EXTRACTED = ["CMP-CHEMBL3353410", "TGT-CHEMBL203"]


# ============================================================ PHASE 1 — INGESTION SCENARIOS
INGESTION_SCENARIOS: list[dict] = [
    # ------------------------------------------------------------------ ING-001
    {
        "scenario_id": "ING-001", "flow": "ingestion", "phase": 1, "path": "full_approval",
        "title": "Load first-line osimertinib evidence — clean upload, curator approves, persisted",
        "final_outcome": "approved_persisted", "required_human_review": False,
        "trigger": {**UI_UPLOAD, "request_id": "ING-001-REQ",
                    "uploaded_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971"],
                    "portal_source_manifest": PORTAL_MANIFEST},
        "stages": [
            {
                "stage": "ingestion_translation", "kind": "agent", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "ingest_and_license_check_uploaded_articles",
                                "portal_source_manifest": PORTAL_MANIFEST,
                                "uploaded_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971"]},
                "input_entities_raw": ACCEPTED_ARTICLES,
                "output_entities": ACCEPTED_ARTICLES,
                "expected_output": {"accepted_article_count": 5, "denied_article_count": 0},
                "decision": "approve", "gate": None,
            },
            {
                "stage": "metadata_linking", "kind": "agent", "agent": "metadata_linking_agent",
                "agent_input": {"task": "extract_entities_and_link_trial_to_regulatory",
                                "retrieved_via_rag": _LINK_TARGETS,
                                "shared_identifiers": ["osimertinib", "Tagrisso", "AZD9291", "NDA208065"]},
                "input_entities": ACCEPTED_ARTICLES,
                "output_entities": _LINKS + _EXTRACTED,
                "expected_output": {"required_links": _LINKS, "extracted_entities": _EXTRACTED},
                "decision": "approve", "gate": None,
            },
            {
                "stage": "curator_approval", "kind": "gate", "actor": CURATOR,
                "gate_record": {"request": "approve_ingested_batch_for_persistence",
                                "reviews_joint_output_of": ["ingestion_translation", "metadata_linking"],
                                "submitted_entities": ACCEPTED_ARTICLES + _LINKS + _EXTRACTED,
                                "reviewer": CURATOR, "decision": "approve"},
                "decision": "approve", "gate": "approved",
            },
            {
                "stage": "persistence", "kind": "sink",
                "persisted": {"target": "cms_knowledge_base", "status": "persisted",
                              "persisted_entities": ACCEPTED_ARTICLES + _LINKS + _EXTRACTED,
                              "persisted_entity_count": len(ACCEPTED_ARTICLES + _LINKS + _EXTRACTED)},
            },
        ],
    },
    # ------------------------------------------------------------------ ING-002
    {
        "scenario_id": "ING-002", "flow": "ingestion", "phase": 1, "path": "guardrail_review",
        "title": "Load with a messy pool — license & PHI guardrails, routed to the curator",
        "final_outcome": "needs_human_review", "required_human_review": True,
        "trigger": {**UI_UPLOAD, "request_id": "ING-002-REQ", "portal_source_manifest": PORTAL_MANIFEST,
                    "uploaded_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971", "PMC4771182"],
                    "candidate_dataset_ids": ["GSE323366", "GSE323365", "GSE272182", "GSE300311", "GSE298111", "GSE297057", "GSE301973"]},
        "stages": [
            {
                "stage": "ingestion_translation", "kind": "agent", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "ingest_and_license_check_uploaded_articles",
                                "portal_source_manifest": PORTAL_MANIFEST,
                                "uploaded_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971", "PMC4771182"],
                                "negative_candidate": {"pmcid": "PMC4771182", "expected": "deny", "decision_entity": "CUR-EXCLUDE-PMC4771182"}},
                "input_entities_raw": ACCEPTED_ARTICLES,
                "output_entities": ACCEPTED_ARTICLES + ["CUR-EXCLUDE-PMC4771182"],
                "expected_output": {"accepted_article_count": 5, "denied_article_count": 1, "denied_pmcids": ["PMC4771182"]},
                "decision": "approve_with_exclusions", "gate": None,
            },
            {
                "stage": "metadata_linking", "kind": "agent", "agent": "metadata_linking_agent",
                "agent_input": {"task": "select_cell_line_or_assay_level_datasets",
                                "candidate_dataset_ids": ["GSE323366", "GSE323365", "GSE272182", "GSE300311", "GSE298111", "GSE297057", "GSE301973"],
                                "negative_candidates": [{"dataset_id": "GSE297057", "expected": "exclude", "decision_entity": "CUR-EXCLUDE-GSE297057"},
                                                        {"dataset_id": "GSE301973", "expected": "exclude", "decision_entity": "CUR-EXCLUDE-GSE301973"}]},
                "input_entities": ACCEPTED_ARTICLES + ["CUR-EXCLUDE-PMC4771182"],
                "output_entities": ACCEPTED_DATASETS + ["CUR-EXCLUDE-GSE297057", "CUR-EXCLUDE-GSE301973"],
                "expected_output": {"accepted_geo_count": 5, "excluded_geo_count": 2},
                "decision": "approve_with_exclusions", "gate": None,
            },
            {
                "stage": "curator_approval", "kind": "gate", "actor": CURATOR,
                "gate_record": {"request": "review_exclusions_before_persistence",
                                "reviews_joint_output_of": ["ingestion_translation", "metadata_linking"],
                                "flagged_exclusions": ["CUR-EXCLUDE-PMC4771182", "CUR-EXCLUDE-GSE297057", "CUR-EXCLUDE-GSE301973"],
                                "reviewer": CURATOR, "decision": "needs_human_review"},
                "decision": "flag_for_human_review", "gate": "denied_pending_human_review",
            },
            {
                "stage": "persistence", "kind": "sink",
                "persisted": {"target": "cms_knowledge_base", "status": "pending_human_review", "persisted_entities": []},
            },
        ],
    },
    # ------------------------------------------------------------------ ING-003
    {
        "scenario_id": "ING-003", "flow": "ingestion", "phase": 1, "path": "synthetic_provenance",
        "title": "Load synthetic ELN/LIMS records — curator approves with required labeling, persisted",
        "final_outcome": "approved_with_required_labeling", "required_human_review": False,
        "trigger": {**UI_UPLOAD, "request_id": "ING-003-REQ", "uploaded_files": SYNTHETIC_RAW,
                    "note": "synthetic ELN/LIMS derived from public GEO structure — no patient data"},
        "stages": [
            {
                "stage": "ingestion_translation", "kind": "agent", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "ingest_synthetic_eln_lims", "uploaded_files": SYNTHETIC_RAW},
                "input_raw": SYNTHETIC_RAW,
                "output_entities": ["SYN-LIMS-001", "SYN-LIMS-010"],
                "expected_output": {"synthetic_record_count": 2, "provenance": "synthetic_from_public_structure"},
                "decision": "approve", "gate": None,
            },
            {
                "stage": "metadata_linking", "kind": "agent", "agent": "metadata_linking_agent",
                "agent_input": {"task": "associate_synthetic_samples_to_geo_series",
                                "samples": ["SYN-LIMS-001", "SYN-LIMS-010"], "retrieved_via_rag": ["DATASET-GSE323366"]},
                "input_entities": ["SYN-LIMS-001", "SYN-LIMS-010"],
                "output_entities": ["DATASET-GSE323366"],
                "expected_output": {"associations": [{"sample": "SYN-LIMS-001", "geo_series": "GSE323366"},
                                                     {"sample": "SYN-LIMS-010", "geo_series": "GSE323366"}]},
                "decision": "approve", "gate": None,
            },
            {
                "stage": "curator_approval", "kind": "gate", "actor": CURATOR,
                "gate_record": {"request": "approve_synthetic_records_with_labeling",
                                "reviews_joint_output_of": ["ingestion_translation", "metadata_linking"],
                                "submitted_entities": ["SYN-LIMS-001", "SYN-LIMS-010", "DATASET-GSE323366"],
                                "required_label": "synthetic_from_public_structure",
                                "reviewer": CURATOR, "decision": "approve_with_required_labeling"},
                "decision": "approve_with_required_labeling", "gate": "approved_with_labeling",
            },
            {
                "stage": "persistence", "kind": "sink",
                "persisted": {"target": "cms_knowledge_base", "status": "persisted_with_required_labeling",
                              "persisted_entities": ["SYN-LIMS-001", "SYN-LIMS-010", "DATASET-GSE323366"],
                              "required_label": "synthetic_from_public_structure"},
            },
        ],
    },
    # ------------------------------------------------------------------ ING-004
    {
        "scenario_id": "ING-004", "flow": "ingestion", "phase": 1, "path": "sensitive_blocked",
        "title": "Load a patient-derived candidate — blocked by the curator, never persisted",
        "final_outcome": "denied_not_persisted", "required_human_review": True,
        "trigger": {**UI_UPLOAD, "request_id": "ING-004-REQ", "candidate_dataset_ids": ["GSE301973"],
                    "note": "GSE301973 references before/after treatment specimens (patient-derived)"},
        "stages": [
            {
                "stage": "ingestion_translation", "kind": "agent", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "ingest_candidate_dataset_metadata_only",
                                "candidate_dataset_ids": ["GSE301973"]},
                "input_raw": ["00_raw/_corpus/txt/agent_inputs/curation_decisions/CUR-EXCLUDE-GSE301973.txt"],
                "output_entities": [],
                "expected_output": {"normalized_count": 0, "flagged_candidate": "GSE301973",
                                    "note": "metadata only — deferred to the knowledge curator"},
                "decision": "defer_to_human_approval", "gate": None,
            },
            {
                "stage": "metadata_linking", "kind": "agent", "agent": "metadata_linking_agent",
                "agent_input": {"task": "link_admitted_entities", "admitted_entities": []},
                "input_entities": [],
                "output_entities": [],
                "expected_output": {"link_count": 0, "reason": "candidate_not_admitted"},
                "decision": "no_action", "gate": None,
            },
            {
                "stage": "curator_approval", "kind": "gate", "actor": CURATOR,
                "gate_record": {"request": "approve_candidate_for_persistence", "candidate": "GSE301973",
                                "reviews_joint_output_of": ["ingestion_translation", "metadata_linking"],
                                "reviewer": CURATOR, "decision": "deny", "reason": "patient_derived_specimens"},
                "decision": "deny", "gate": "denied",
            },
            {
                "stage": "persistence", "kind": "sink",
                "persisted": {"target": "cms_knowledge_base", "status": "denied_not_persisted", "persisted_entities": []},
            },
        ],
    },
]


# ============================================================ PHASE 2 — SEARCH SCENARIOS
SEARCH_SCENARIOS: list[dict] = [
    # ------------------------------------------------------------------ QRY-001
    {
        "scenario_id": "QRY-001", "flow": "search", "phase": 2, "path": "no_data", "kb_state": "empty",
        "title": "Query an empty knowledge base — no grounded answer (demo step 1)",
        "final_outcome": "no_grounded_answer", "required_human_review": False,
        "trigger": {**UI_QUERY, "request_id": "QRY-001-REQ", "query": _GROUNDED_QUERY, "kb_state": "empty"},
        "stages": [
            {
                "stage": "search_chat", "kind": "agent", "agent": "search_chat_agent",
                "agent_input": {"task": "answer_grounded_query", "query": _GROUNDED_QUERY, "retrieval_scope_entities": []},
                "prompt": _GROUNDED_QUERY,
                "input_entities": [],
                "output_entities": [],
                "expected_output": {"decision": "no_grounded_answer", "reason": "knowledge_base_empty", "citation_count": 0},
                "decision": "no_grounded_answer", "gate": None,
            },
            {
                "stage": "curation_compliance", "kind": "agent", "agent": "curation_compliance_agent",
                "agent_input": {"task": "review_answer_before_return", "draft_decision": "no_grounded_answer"},
                "input_entities": [],
                "output_entities": [],
                "expected_output": {"decision": "confirm_no_data_response", "sensitive_content_found": False, "required_human_review": False},
                "decision": "confirm_no_data_response", "gate": None,
            },
            {
                "stage": "compliance_approval", "kind": "gate", "actor": COMPLIANCE_OWNER,
                "gate_record": {"request": "approve_response_before_return",
                                "reviews_joint_output_of": ["search_chat", "curation_compliance"],
                                "reviewer": COMPLIANCE_OWNER, "decision": "approve"},
                "decision": "approve", "gate": "approved",
            },
            {
                "stage": "response", "kind": "output",
                "response": {"returned": "No grounded information is available yet — ingest knowledge first.",
                             "citations": [], "raw_source_trace": False},
            },
        ],
    },
    # ------------------------------------------------------------------ QRY-002
    {
        "scenario_id": "QRY-002", "flow": "search", "phase": 2, "path": "grounded", "kb_state": "populated",
        "title": "Query a populated knowledge base — grounded answer with citations (demo step 3)",
        "final_outcome": "answer_with_citations", "required_human_review": False,
        "trigger": {**UI_QUERY, "request_id": "QRY-002-REQ", "query": _GROUNDED_QUERY, "kb_state": "populated",
                    "depends_on_ingestion": "ING-001 (entities must already be persisted)"},
        "stages": [
            {
                "stage": "search_chat", "kind": "agent", "agent": "search_chat_agent",
                "agent_input": {"task": "answer_grounded_query", "query": _GROUNDED_QUERY,
                                "retrieval_scope_entities": _RETRIEVAL_SCOPE},
                "prompt": _GROUNDED_QUERY,
                "input_entities": _RETRIEVAL_SCOPE,
                "output_entities": [],
                "expected_output": {"decision": "answer_with_citations", "minimum_citation_count": 2,
                                    "must_include_raw_source_trace": True,
                                    "expected_citations": _GROUNDED_CITATIONS, "expected_answer_points": _ANSWER_POINTS},
                "decision": "answer_with_citations", "gate": None,
            },
            {
                "stage": "curation_compliance", "kind": "agent", "agent": "curation_compliance_agent",
                "agent_input": {"task": "review_answer_before_return", "draft_answer_citations": _GROUNDED_CITATIONS,
                                "enforced_policy_refs": ["HLS-TRIAL-300", "HLS-LIC-200"]},
                "input_entities": _GROUNDED_CITATIONS,
                "output_entities": [],
                "expected_output": {"decision": "approve_response", "sensitive_content_found": False, "required_human_review": False},
                "decision": "approve_response", "gate": None,
            },
            {
                "stage": "compliance_approval", "kind": "gate", "actor": COMPLIANCE_OWNER,
                "gate_record": {"request": "approve_response_before_return",
                                "reviews_joint_output_of": ["search_chat", "curation_compliance"],
                                "approved_citations": _GROUNDED_CITATIONS,
                                "reviewer": COMPLIANCE_OWNER, "decision": "approve"},
                "decision": "approve", "gate": "approved",
            },
            {
                "stage": "response", "kind": "output",
                "response": {"returned": "Grounded answer on osimertinib resistance mechanisms and first-line evidence.",
                             "citations": _GROUNDED_CITATIONS, "raw_source_trace": True},
            },
        ],
    },
]

SCENARIOS: list[dict] = INGESTION_SCENARIOS + SEARCH_SCENARIOS
SCENARIOS_BY_ID: dict[str, dict] = {s["scenario_id"]: s for s in SCENARIOS}


def scenario_folder(scenario: dict) -> str:
    """e.g. 'ING-001_full_approval' / 'QRY-001_no_data' — mirrors loan's APP-XXX_<reason>."""
    return f"{scenario['scenario_id']}_{scenario['path']}"


def materialized_stages(scenario: dict) -> list[dict]:
    """The stages that get a raw-layer folder (data-consuming agents + the response output)."""
    return [s for s in scenario["stages"] if s["kind"] in MATERIALIZED_KINDS]


def stage_folder(scenario: dict, stage: dict) -> str:
    """'NN_<stage>' — NN is the 1-based position among the scenario's materialized stages."""
    idx = materialized_stages(scenario).index(stage) + 1
    return f"{idx:02d}_{stage['stage']}"


def stage_primary(stage: dict) -> tuple[str | None, str, object]:
    """Return (filename, key, content) for a stage's primary payload, keyed by its kind."""
    filename, key = STAGE_PRIMARY[stage["kind"]]
    return filename, key, stage[key]


def demo_prefix(scenario_id: str) -> int:
    """1-based position of a scenario in the headline DEMO_SCENARIO sequence."""
    return DEMO_SEQUENCE.index(scenario_id) + 1
