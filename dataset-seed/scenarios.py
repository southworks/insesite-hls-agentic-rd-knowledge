#!/usr/bin/env python3
"""
Scenario definitions for the HLS Agentic R&D knowledge mining dataset.

Single source of truth for the test cases. HLS is **two isolated processes**, not one
continuous 4-agent flow with an orchestrator. They are decoupled in time:

  1. INGESTION (load knowledge):  upload -> ingestion/translation -> metadata linking
                                  -> human approval -> persistence into the CMS/knowledge base
  2. SEARCH    (query knowledge): UI query -> search/chat retrieval
                                  -> curation/compliance review of the result -> response

The entry point of each flow is a controlled UI action (a button/process trigger), not a
free-form chatbot. In ingestion, manual file upload stands in for the "Connect Portals" MCP:
the uploaded assets *represent* data that could come from external medical portals (a
conceptual external connector, not the literal demo interaction). The search flow runs later,
against already-persisted knowledge — there is no runtime coupling between the two.

Both generators import this module so the 09 rollups and the per-scenario Raw-Layer folders
stay aligned:

  - generate_normalized_layers.py  -> 09_decision_ground_truth/{ING,QRY}-XXX.json
  - build_scenario_folders.py      -> 00_raw/{ING,QRY}-XXX_<path>/<stage>/...

Trackable prefixes: ING-### (ingestion) and QRY-### (query/search), like APP-XXX in loan.

Each stage declares:
  - stage / kind / agent    : the step, its kind (trigger|agent|gate|sink|output), and the
                              agent capability where kind == "agent"
  - <primary payload>       : trigger | agent_input | gate_record | persisted | response,
                              keyed by kind (see STAGE_PRIMARY) — written as the stage's main file
  - input_entities          : normalized entity ids handed in from upstream (copied to input/)
  - input_raw               : explicit corpus-relative raw files (copied to input/)
  - input_entities_raw      : copy the raw_sources of these entities into input/ (uploaded docs)
  - input_from_output_raw   : if true, input/ also gets the raw_sources of output_entities
  - output_entities         : normalized entity ids this stage would produce (copied to expected_output/)
  - expected_output         : measurable expectations for this stage
  - decision / gate         : the stage decision and the HITL gate result where applicable
"""

from __future__ import annotations

# Stage folder names. Keys are unique across both flows, so one map serves both.
STAGE_FOLDERS = {
    # ingestion flow
    "upload": "01_upload",
    "ingestion_translation": "02_ingestion_translation",
    "metadata_linking": "03_metadata_linking",
    "human_approval": "04_human_approval",
    "persistence": "05_persistence",
    # search flow
    "query": "01_query",
    "search_chat": "02_search_chat",
    "curation_compliance": "03_curation_compliance",
    "response": "04_response",
}

# kind -> (primary filename, payload key on the stage dict)
STAGE_PRIMARY = {
    "trigger": ("trigger.json", "trigger"),
    "agent": ("agent_input.json", "agent_input"),
    "gate": ("gate.json", "gate_record"),
    "sink": ("persisted.json", "persisted"),
    "output": ("response.json", "response"),
}

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
_LINKS = ["LINK-FLAURA-REG-NDA208065", "LINK-REG-NDA208065-LBL-TAGRISSO-OPENFDA"]
_EXTRACTED = ["CMP-CHEMBL3353410", "TGT-CHEMBL203"]


# ==================================================================== INGESTION FLOW
INGESTION_SCENARIOS: list[dict] = [
    # ------------------------------------------------------------------ ING-001
    {
        "scenario_id": "ING-001", "flow": "ingestion", "path": "full_approval",
        "title": "Load first-line osimertinib evidence — clean upload, full approval, persisted",
        "final_outcome": "approved_persisted", "required_human_review": False,
        "trigger": {**UI_UPLOAD, "request_id": "ING-001-REQ",
                    "uploaded_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971"]},
        "stages": [
            {
                "stage": "upload", "kind": "trigger",
                "trigger": {**UI_UPLOAD, "uploaded_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971"],
                            "portal_source_manifest": PORTAL_MANIFEST},
                "input_entities_raw": ACCEPTED_ARTICLES,
            },
            {
                "stage": "ingestion_translation", "kind": "agent", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "ingest_and_license_check_uploaded_articles", "portal_source_manifest": PORTAL_MANIFEST,
                                "uploaded_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971"]},
                "input_from_output_raw": True, "output_entities": ACCEPTED_ARTICLES,
                "expected_output": {"accepted_article_count": 5, "denied_article_count": 0},
                "decision": "approve", "gate": None,
            },
            {
                "stage": "metadata_linking", "kind": "agent", "agent": "metadata_linking_agent",
                "agent_input": {"task": "extract_entities_and_link_trial_to_regulatory",
                                "normalized_entities": ["TRIAL-NCT02296125", "TRIAL-NCT02151981", "REG-NDA208065", "LBL-TAGRISSO-OPENFDA"],
                                "shared_identifiers": ["osimertinib", "Tagrisso", "AZD9291", "NDA208065"]},
                "input_entities": ["TRIAL-NCT02296125", "TRIAL-NCT02151981", "REG-NDA208065", "LBL-TAGRISSO-OPENFDA"],
                "output_entities": _LINKS + _EXTRACTED,
                "expected_output": {"required_links": _LINKS, "extracted_entities": _EXTRACTED},
                "decision": "approve", "gate": None,
            },
            {
                "stage": "human_approval", "kind": "gate",
                "gate_record": {"request": "approve_ingested_batch_for_persistence",
                                "submitted_entities": ACCEPTED_ARTICLES + _LINKS + _EXTRACTED,
                                "reviewer": "Marisol Vega, Research Data Steward (fictional)", "decision": "approve"},
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
        "scenario_id": "ING-002", "flow": "ingestion", "path": "guardrail_review",
        "title": "Load with a messy pool — license & PHI guardrails, routed to human review",
        "final_outcome": "needs_human_review", "required_human_review": True,
        "trigger": {**UI_UPLOAD, "request_id": "ING-002-REQ",
                    "uploaded_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971", "PMC4771182"],
                    "candidate_dataset_ids": ["GSE323366", "GSE323365", "GSE272182", "GSE300311", "GSE298111", "GSE297057", "GSE301973"]},
        "stages": [
            {
                "stage": "upload", "kind": "trigger",
                "trigger": {**UI_UPLOAD, "portal_source_manifest": PORTAL_MANIFEST,
                            "uploaded_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971", "PMC4771182"],
                            "candidate_dataset_ids": ["GSE323366", "GSE323365", "GSE272182", "GSE300311", "GSE298111", "GSE297057", "GSE301973"]},
                "input_entities_raw": ACCEPTED_ARTICLES,
            },
            {
                "stage": "ingestion_translation", "kind": "agent", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "ingest_and_license_check_uploaded_articles", "portal_source_manifest": PORTAL_MANIFEST,
                                "uploaded_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971", "PMC4771182"],
                                "negative_candidate": {"pmcid": "PMC4771182", "expected": "deny", "decision_entity": "CUR-EXCLUDE-PMC4771182"}},
                "input_from_output_raw": True, "output_entities": ACCEPTED_ARTICLES + ["CUR-EXCLUDE-PMC4771182"],
                "expected_output": {"accepted_article_count": 5, "denied_article_count": 1, "denied_pmcids": ["PMC4771182"]},
                "decision": "approve_with_exclusions", "gate": None,
            },
            {
                "stage": "metadata_linking", "kind": "agent", "agent": "metadata_linking_agent",
                "agent_input": {"task": "select_cell_line_or_assay_level_datasets",
                                "candidate_dataset_ids": ["GSE323366", "GSE323365", "GSE272182", "GSE300311", "GSE298111", "GSE297057", "GSE301973"],
                                "negative_candidates": [{"dataset_id": "GSE297057", "expected": "exclude", "decision_entity": "CUR-EXCLUDE-GSE297057"},
                                                        {"dataset_id": "GSE301973", "expected": "exclude", "decision_entity": "CUR-EXCLUDE-GSE301973"}]},
                "input_entities": ACCEPTED_DATASETS,
                "output_entities": ACCEPTED_DATASETS + ["CUR-EXCLUDE-GSE297057", "CUR-EXCLUDE-GSE301973"],
                "expected_output": {"accepted_geo_count": 5, "excluded_geo_count": 2},
                "decision": "approve_with_exclusions", "gate": None,
            },
            {
                "stage": "human_approval", "kind": "gate",
                "gate_record": {"request": "review_exclusions_before_persistence",
                                "flagged_exclusions": ["CUR-EXCLUDE-PMC4771182", "CUR-EXCLUDE-GSE297057", "CUR-EXCLUDE-GSE301973"],
                                "reviewer": "Marisol Vega, Research Data Steward (fictional)", "decision": "needs_human_review"},
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
        "scenario_id": "ING-003", "flow": "ingestion", "path": "synthetic_provenance",
        "title": "Load synthetic ELN/LIMS records — approve with required labeling, persisted",
        "final_outcome": "approved_with_required_labeling", "required_human_review": False,
        "trigger": {**UI_UPLOAD, "request_id": "ING-003-REQ", "uploaded_files": SYNTHETIC_RAW},
        "stages": [
            {
                "stage": "upload", "kind": "trigger",
                "trigger": {**UI_UPLOAD, "uploaded_files": SYNTHETIC_RAW,
                            "note": "synthetic ELN/LIMS derived from public GEO structure — no patient data"},
                "input_raw": SYNTHETIC_RAW,
            },
            {
                "stage": "ingestion_translation", "kind": "agent", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "ingest_synthetic_eln_lims", "uploaded_files": SYNTHETIC_RAW},
                "input_raw": SYNTHETIC_RAW, "output_entities": ["SYN-LIMS-001", "SYN-LIMS-010"],
                "expected_output": {"synthetic_record_count": 2, "provenance": "synthetic_from_public_structure"},
                "decision": "approve", "gate": None,
            },
            {
                "stage": "metadata_linking", "kind": "agent", "agent": "metadata_linking_agent",
                "agent_input": {"task": "associate_synthetic_samples_to_geo_series", "samples": ["SYN-LIMS-001", "SYN-LIMS-010"]},
                "input_entities": ["SYN-LIMS-001", "SYN-LIMS-010"], "output_entities": ["DATASET-GSE323366"],
                "expected_output": {"associations": [{"sample": "SYN-LIMS-001", "geo_series": "GSE323366"},
                                                     {"sample": "SYN-LIMS-010", "geo_series": "GSE323366"}]},
                "decision": "approve", "gate": None,
            },
            {
                "stage": "human_approval", "kind": "gate",
                "gate_record": {"request": "approve_synthetic_records_with_labeling",
                                "submitted_entities": ["SYN-LIMS-001", "SYN-LIMS-010"],
                                "required_label": "synthetic_from_public_structure",
                                "reviewer": "Marisol Vega, Research Data Steward (fictional)", "decision": "approve_with_required_labeling"},
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
        "scenario_id": "ING-004", "flow": "ingestion", "path": "sensitive_blocked",
        "title": "Load a patient-derived candidate — blocked at human approval, never persisted",
        "final_outcome": "denied_not_persisted", "required_human_review": True,
        "trigger": {**UI_UPLOAD, "request_id": "ING-004-REQ", "candidate_dataset_ids": ["GSE301973"]},
        "stages": [
            {
                "stage": "upload", "kind": "trigger",
                "trigger": {**UI_UPLOAD, "portal_source_manifest": PORTAL_MANIFEST, "candidate_dataset_ids": ["GSE301973"],
                            "note": "GSE301973 references before/after treatment specimens (patient-derived)"},
                "input_raw": ["00_raw/_corpus/txt/agent_inputs/curation_decisions/CUR-EXCLUDE-GSE301973.txt"],
            },
            {
                "stage": "ingestion_translation", "kind": "agent", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "ingest_candidate_dataset_metadata_only", "portal_source_manifest": PORTAL_MANIFEST,
                                "candidate_dataset_ids": ["GSE301973"]},
                "input_raw": ["00_raw/_corpus/txt/agent_inputs/curation_decisions/CUR-EXCLUDE-GSE301973.txt"],
                "output_entities": [],
                "expected_output": {"normalized_count": 0, "flagged_candidate": "GSE301973", "note": "metadata only — deferred to human approval"},
                "decision": "defer_to_human_approval", "gate": None,
            },
            {
                "stage": "metadata_linking", "kind": "agent", "agent": "metadata_linking_agent",
                "agent_input": {"task": "link_admitted_entities", "admitted_entities": []},
                "input_entities": [], "output_entities": [],
                "expected_output": {"link_count": 0, "reason": "candidate_not_admitted"},
                "decision": "no_action", "gate": None,
            },
            {
                "stage": "human_approval", "kind": "gate",
                "gate_record": {"request": "approve_candidate_for_persistence", "candidate": "GSE301973",
                                "reviewer": "Marisol Vega, Research Data Steward (fictional)",
                                "decision": "deny", "reason": "patient_derived_specimens"},
                "decision": "deny", "gate": "denied",
            },
            {
                "stage": "persistence", "kind": "sink",
                "persisted": {"target": "cms_knowledge_base", "status": "denied_not_persisted", "persisted_entities": []},
            },
        ],
    },
]


# ==================================================================== SEARCH FLOW
SEARCH_SCENARIOS: list[dict] = [
    # ------------------------------------------------------------------ QRY-001
    {
        "scenario_id": "QRY-001", "flow": "search", "path": "no_data", "kb_state": "empty",
        "title": "Query an empty knowledge base — no grounded answer (demo step 1)",
        "final_outcome": "no_grounded_answer", "required_human_review": False,
        "trigger": {**UI_QUERY, "request_id": "QRY-001-REQ", "query": _GROUNDED_QUERY, "kb_state": "empty"},
        "stages": [
            {
                "stage": "query", "kind": "trigger",
                "trigger": {**UI_QUERY, "query": _GROUNDED_QUERY, "kb_state": "empty"},
            },
            {
                "stage": "search_chat", "kind": "agent", "agent": "search_chat_agent",
                "agent_input": {"task": "answer_grounded_query", "query": _GROUNDED_QUERY, "retrieval_scope_entities": []},
                "input_entities": [],
                "expected_output": {"decision": "no_grounded_answer", "reason": "knowledge_base_empty", "citation_count": 0},
                "decision": "no_grounded_answer", "gate": None,
            },
            {
                "stage": "curation_compliance", "kind": "agent", "agent": "curation_compliance_agent",
                "agent_input": {"task": "review_answer_before_return", "draft_decision": "no_grounded_answer"},
                "input_entities": [],
                "expected_output": {"decision": "confirm_no_data_response", "sensitive_content_found": False, "required_human_review": False},
                "decision": "confirm_no_data_response", "gate": "approved",
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
        "scenario_id": "QRY-002", "flow": "search", "path": "grounded", "kb_state": "populated",
        "title": "Query a populated knowledge base — grounded answer with citations (demo step 3)",
        "final_outcome": "answer_with_citations", "required_human_review": False,
        "trigger": {**UI_QUERY, "request_id": "QRY-002-REQ", "query": _GROUNDED_QUERY, "kb_state": "populated"},
        "stages": [
            {
                "stage": "query", "kind": "trigger",
                "trigger": {**UI_QUERY, "query": _GROUNDED_QUERY, "kb_state": "populated",
                            "depends_on_ingestion": "ING-001 (entities must already be persisted)"},
            },
            {
                "stage": "search_chat", "kind": "agent", "agent": "search_chat_agent",
                "agent_input": {"task": "answer_grounded_query", "query": _GROUNDED_QUERY, "retrieval_scope_entities": _RETRIEVAL_SCOPE},
                "input_entities": _RETRIEVAL_SCOPE,
                "expected_output": {"decision": "answer_with_citations", "minimum_citation_count": 2, "must_include_raw_source_trace": True,
                                    "expected_citations": _GROUNDED_CITATIONS, "expected_answer_points": _ANSWER_POINTS},
                "decision": "answer_with_citations", "gate": None,
            },
            {
                "stage": "curation_compliance", "kind": "agent", "agent": "curation_compliance_agent",
                "agent_input": {"task": "review_answer_before_return", "draft_answer_citations": _GROUNDED_CITATIONS,
                                "enforced_policy_refs": ["HLS-TRIAL-300", "HLS-LIC-200"]},
                "input_entities": _GROUNDED_CITATIONS,
                "expected_output": {"decision": "approve_response", "sensitive_content_found": False, "required_human_review": False},
                "decision": "approve_response", "gate": "approved",
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


def scenario_folder(scenario: dict) -> str:
    """e.g. 'ING-001_full_approval' / 'QRY-001_no_data' — mirrors loan's APP-XXX_<reason>."""
    return f"{scenario['scenario_id']}_{scenario['path']}"


def stage_folder(stage: dict) -> str:
    return STAGE_FOLDERS[stage["stage"]]


def stage_primary(stage: dict) -> tuple[str, str, dict]:
    """Return (filename, key, content) for a stage's primary file, keyed by its kind."""
    filename, key = STAGE_PRIMARY[stage["kind"]]
    return filename, key, stage[key]
