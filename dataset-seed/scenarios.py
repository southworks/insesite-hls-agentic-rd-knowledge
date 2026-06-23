#!/usr/bin/env python3
"""
End-to-end scenario definitions for the HLS Agentic R&D knowledge mining dataset.

Single source of truth for the e2e *test cases*. Each scenario is one full path
through the workflow (Orchestrator -> Ingestion -> Metadata/Linking -> Search/Chat
-> Curation/Compliance) and differs from the others at the human-in-the-loop gates.

Both generators import this module so the 09 ground-truth rollups and the per-scenario
Raw-Layer folders stay aligned:

  - generate_normalized_layers.py  -> 09_decision_ground_truth/RKM-XXX.json (e2e rollup)
  - build_scenario_folders.py      -> 00_raw/RKM-XXX_<path>/<stage>/{input,expected_output}/

Mirrors the `scenario_layout.py` shared-module pattern used by loan-mortgage-agents,
adapted to HLS's 4-agent chain (entity-based, so the canonical corpus in
00_raw/_corpus/ stays the single source of truth and scenario folders duplicate it).

Trackable prefix: RKM-### (R&D Knowledge Mining), like APP-XXX in loan.

Each stage declares:
  - stage / agent           : the workflow step and the agent capability
  - agent_input             : structured payload to START this stage in isolation
  - input_entities          : normalized entity ids handed in from upstream (copied to input/)
  - input_raw               : explicit corpus-relative raw files (copied to input/)
  - input_from_output_raw   : if true, input/ also gets the raw_sources of output_entities
                              (used by the ingestion stage: it ingests the raw behind its output)
  - output_entities         : normalized entity ids this stage would produce (copied to expected_output/)
  - expected_output         : measurable expectations for this stage
  - decision / gate         : the stage decision and the HITL gate result (approved | denied |
                              denied_pending_human_review | approved_with_labeling | None)
"""

from __future__ import annotations

# Workflow stage order (folder names under each scenario). The orchestrator is stage 01.
STAGE_FOLDERS = {
    "orchestrator": "01_orchestrator",
    "ingestion_translation": "02_ingestion_translation",
    "metadata_linking": "03_metadata_linking",
    "search_chat": "04_search_chat",
    "curation_compliance": "05_curation_compliance",
}

PORTAL_MANIFEST = "00_raw/_corpus/csv/partner_vendor_repositories/partner_vendor_repository_index.csv"
SYNTHETIC_RAW = [
    "00_raw/_corpus/csv/synthetic_eln_lims/lims_sample_manifest.csv",
    "00_raw/_corpus/txt/synthetic_eln_lims/eln_experiment_notebook.txt",
    "00_raw/_corpus/txt/synthetic_eln_lims/lims_quality_control_report.txt",
]

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

ACCEPTED_ARTICLES = ["RDOC-PMC6889286", "RDOC-PMC5447962", "RDOC-PMC13070087", "RDOC-PMC13129538", "RDOC-PMC13143971"]
ACCEPTED_DATASETS = ["DATASET-GSE323366", "DATASET-GSE323365", "DATASET-GSE272182", "DATASET-GSE300311", "DATASET-GSE298111"]


SCENARIOS: list[dict] = [
    # ------------------------------------------------------------------ RKM-001
    {
        "scenario_id": "RKM-001",
        "path": "full_approval",
        "title": "First-line osimertinib evidence assembly — full approval (happy path)",
        "final_outcome": "approved",
        "required_human_review": False,
        "orchestrator_request": {
            "request_id": "RKM-001-REQ",
            "intent": "assemble_first_line_osimertinib_evidence",
            "topic": "osimertinib / Tagrisso / AZD9291 in EGFR-mutated NSCLC",
            "routed_to": ["ingestion_translation_agent", "metadata_linking_agent", "search_chat_agent", "curation_compliance_agent"],
        },
        "stages": [
            {
                "stage": "ingestion_translation", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "connect_portals_and_license_check_articles", "portal_source_manifest": PORTAL_MANIFEST,
                                "candidate_article_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971"]},
                "input_entities": [], "input_from_output_raw": True,
                "output_entities": ACCEPTED_ARTICLES,
                "expected_output": {"accepted_article_count": 5, "denied_article_count": 0},
                "decision": "approve", "gate": None,
            },
            {
                "stage": "metadata_linking", "agent": "metadata_linking_agent",
                "agent_input": {"task": "extract_entities_and_link_trial_to_regulatory",
                                "normalized_entities": ["TRIAL-NCT02296125", "TRIAL-NCT02151981", "REG-NDA208065", "LBL-TAGRISSO-OPENFDA"],
                                "shared_identifiers": ["osimertinib", "Tagrisso", "AZD9291", "NDA208065"]},
                "input_entities": ["TRIAL-NCT02296125", "TRIAL-NCT02151981", "REG-NDA208065", "LBL-TAGRISSO-OPENFDA"],
                "output_entities": ["LINK-FLAURA-REG-NDA208065", "LINK-REG-NDA208065-LBL-TAGRISSO-OPENFDA", "CMP-CHEMBL3353410", "TGT-CHEMBL203"],
                "expected_output": {"required_links": ["LINK-FLAURA-REG-NDA208065", "LINK-REG-NDA208065-LBL-TAGRISSO-OPENFDA"],
                                    "extracted_entities": ["CMP-CHEMBL3353410", "TGT-CHEMBL203"]},
                "decision": "approve", "gate": "approved",
            },
            {
                "stage": "search_chat", "agent": "search_chat_agent",
                "agent_input": {"task": "answer_grounded_query", "query": _GROUNDED_QUERY,
                                "retrieval_scope_entities": ["RDOC-PMC6889286", "TRIAL-NCT02296125", "DATASET-GSE323366", "LBL-TAGRISSO-OPENFDA"]},
                "input_entities": ["RDOC-PMC6889286", "TRIAL-NCT02296125", "DATASET-GSE323366", "LBL-TAGRISSO-OPENFDA"],
                "output_entities": [],
                "expected_output": {"decision": "answer_with_citations", "minimum_citation_count": 2, "must_include_raw_source_trace": True,
                                    "expected_citations": ["RDOC-PMC6889286", "TRIAL-NCT02296125", "LBL-TAGRISSO-OPENFDA"],
                                    "expected_answer_points": _ANSWER_POINTS},
                "decision": "answer_with_citations", "gate": None,
            },
            {
                "stage": "curation_compliance", "agent": "curation_compliance_agent",
                "agent_input": {"task": "flag_sensitive_content_and_capture_decisions",
                                "records_under_review": ACCEPTED_ARTICLES, "enforced_policy_refs": ["HLS-DATA-100", "HLS-LIC-200"]},
                "input_entities": ACCEPTED_ARTICLES,
                "output_entities": ["CUR-PMC6889286", "CUR-PMC5447962", "CUR-PMC13070087", "CUR-PMC13129538", "CUR-PMC13143971"],
                "expected_output": {"decision": "approve", "excluded_count": 0, "required_human_review": False},
                "decision": "approve", "gate": "approved",
            },
        ],
    },
    # ------------------------------------------------------------------ RKM-002
    {
        "scenario_id": "RKM-002",
        "path": "guardrail_review",
        "title": "License & PHI guardrail — denials/exclusions routed to human review",
        "final_outcome": "needs_human_review",
        "required_human_review": True,
        "orchestrator_request": {
            "request_id": "RKM-002-REQ",
            "intent": "assemble_evidence_with_unverified_candidates",
            "topic": "osimertinib evidence with a no-license article and patient-derived datasets in the candidate pool",
            "routed_to": ["ingestion_translation_agent", "metadata_linking_agent", "search_chat_agent", "curation_compliance_agent"],
        },
        "stages": [
            {
                "stage": "ingestion_translation", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "connect_portals_and_license_check_articles", "portal_source_manifest": PORTAL_MANIFEST,
                                "candidate_article_pmcids": ["PMC6889286", "PMC5447962", "PMC13070087", "PMC13129538", "PMC13143971", "PMC4771182"],
                                "negative_candidate": {"pmcid": "PMC4771182", "expected": "deny", "decision_entity": "CUR-EXCLUDE-PMC4771182"}},
                "input_entities": [], "input_from_output_raw": True,
                "output_entities": ACCEPTED_ARTICLES + ["CUR-EXCLUDE-PMC4771182"],
                "expected_output": {"accepted_article_count": 5, "denied_article_count": 1, "denied_pmcids": ["PMC4771182"]},
                "decision": "approve_with_exclusions", "gate": None,
            },
            {
                "stage": "metadata_linking", "agent": "metadata_linking_agent",
                "agent_input": {"task": "select_cell_line_or_assay_level_datasets",
                                "candidate_dataset_ids": ["GSE323366", "GSE323365", "GSE272182", "GSE300311", "GSE298111", "GSE297057", "GSE301973"],
                                "negative_candidates": [{"dataset_id": "GSE297057", "expected": "exclude", "decision_entity": "CUR-EXCLUDE-GSE297057"},
                                                        {"dataset_id": "GSE301973", "expected": "exclude", "decision_entity": "CUR-EXCLUDE-GSE301973"}]},
                "input_entities": ACCEPTED_DATASETS,
                "output_entities": ACCEPTED_DATASETS + ["CUR-EXCLUDE-GSE297057", "CUR-EXCLUDE-GSE301973"],
                "expected_output": {"accepted_geo_count": 5, "excluded_geo_count": 2},
                "decision": "approve_with_exclusions", "gate": "approved",
            },
            {
                "stage": "search_chat", "agent": "search_chat_agent",
                "agent_input": {"task": "answer_grounded_query", "query": _GROUNDED_QUERY,
                                "retrieval_scope_entities": ["RDOC-PMC6889286", "TRIAL-NCT02296125", "DATASET-GSE323366", "LBL-TAGRISSO-OPENFDA"],
                                "constraint": "only_admitted_corpus"},
                "input_entities": ["RDOC-PMC6889286", "TRIAL-NCT02296125", "DATASET-GSE323366", "LBL-TAGRISSO-OPENFDA"],
                "output_entities": [],
                "expected_output": {"decision": "answer_with_citations", "minimum_citation_count": 2, "must_include_raw_source_trace": True,
                                    "expected_citations": ["RDOC-PMC6889286", "TRIAL-NCT02296125", "LBL-TAGRISSO-OPENFDA"],
                                    "must_exclude_entities": ["GSE297057", "GSE301973", "PMC4771182"]},
                "decision": "answer_with_citations", "gate": None,
            },
            {
                "stage": "curation_compliance", "agent": "curation_compliance_agent",
                "agent_input": {"task": "flag_sensitive_content_and_capture_decisions",
                                "records_under_review": ["PMC4771182", "GSE297057", "GSE301973"], "enforced_policy_refs": ["HLS-DATA-100", "HLS-GEO-400", "HLS-LIC-200"]},
                "input_entities": ["CUR-EXCLUDE-PMC4771182", "CUR-EXCLUDE-GSE297057", "CUR-EXCLUDE-GSE301973"],
                "output_entities": ["CUR-EXCLUDE-PMC4771182", "CUR-EXCLUDE-GSE297057", "CUR-EXCLUDE-GSE301973"],
                "expected_output": {"decision": "flag_for_human_review", "flagged_count": 3, "required_human_review": True},
                "decision": "flag_for_human_review", "gate": "denied_pending_human_review",
            },
        ],
    },
    # ------------------------------------------------------------------ RKM-003
    {
        "scenario_id": "RKM-003",
        "path": "synthetic_provenance",
        "title": "Synthetic ELN/LIMS provenance — approve with required labeling",
        "final_outcome": "approved_with_required_labeling",
        "required_human_review": False,
        "orchestrator_request": {
            "request_id": "RKM-003-REQ",
            "intent": "ingest_synthetic_operational_records",
            "topic": "synthetic ELN/LIMS sample manifest derived from public GEO structure",
            "routed_to": ["ingestion_translation_agent", "metadata_linking_agent", "search_chat_agent", "curation_compliance_agent"],
        },
        "stages": [
            {
                "stage": "ingestion_translation", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "ingest_synthetic_eln_lims", "raw_records": SYNTHETIC_RAW},
                "input_entities": [], "input_raw": SYNTHETIC_RAW,
                "output_entities": ["SYN-LIMS-001", "SYN-LIMS-010"],
                "expected_output": {"synthetic_record_count": 2, "provenance": "synthetic_from_public_structure"},
                "decision": "approve", "gate": None,
            },
            {
                "stage": "metadata_linking", "agent": "metadata_linking_agent",
                "agent_input": {"task": "associate_synthetic_samples_to_geo_series", "samples": ["SYN-LIMS-001", "SYN-LIMS-010"]},
                "input_entities": ["SYN-LIMS-001", "SYN-LIMS-010"],
                "output_entities": ["DATASET-GSE323366"],
                "expected_output": {"associations": [{"sample": "SYN-LIMS-001", "geo_series": "GSE323366"},
                                                     {"sample": "SYN-LIMS-010", "geo_series": "GSE323366"}]},
                "decision": "approve", "gate": "approved",
            },
            {
                "stage": "search_chat", "agent": "search_chat_agent",
                "agent_input": {"task": "answer_grounded_query",
                                "query": "Which synthetic ELN/LIMS samples map to the PC9/H1650 EGFR-inhibition GEO series, and what is their provenance?",
                                "retrieval_scope_entities": ["SYN-LIMS-001", "SYN-LIMS-010", "DATASET-GSE323366"]},
                "input_entities": ["SYN-LIMS-001", "SYN-LIMS-010", "DATASET-GSE323366"],
                "output_entities": [],
                "expected_output": {"decision": "answer_with_citations", "minimum_citation_count": 2, "must_include_raw_source_trace": True,
                                    "must_state_synthetic_provenance": "synthetic_from_public_structure",
                                    "expected_citations": ["SYN-LIMS-001", "DATASET-GSE323366"]},
                "decision": "answer_with_citations", "gate": None,
            },
            {
                "stage": "curation_compliance", "agent": "curation_compliance_agent",
                "agent_input": {"task": "verify_synthetic_provenance_labeling",
                                "records_under_review": ["SYN-LIMS-001", "SYN-LIMS-010"], "enforced_policy_refs": ["HLS-DATA-110"]},
                "input_entities": ["SYN-LIMS-001", "SYN-LIMS-010"],
                "output_entities": ["CUR-SYNTHETIC-ELN-LIMS"],
                "expected_output": {"decision": "approve_with_required_labeling",
                                    "synthetic_records_must_include_provenance": "synthetic_from_public_structure"},
                "decision": "approve_with_required_labeling", "gate": "approved_with_labeling",
            },
        ],
    },
    # ------------------------------------------------------------------ RKM-004
    {
        "scenario_id": "RKM-004",
        "path": "curation_denied",
        "title": "Sensitive content blocked — denied at the curation gate",
        "final_outcome": "denied",
        "required_human_review": True,
        "orchestrator_request": {
            "request_id": "RKM-004-REQ",
            "intent": "ingest_candidate_dataset",
            "topic": "patient-derived GEO candidate GSE301973 (before/after treatment specimens)",
            "routed_to": ["ingestion_translation_agent", "metadata_linking_agent", "search_chat_agent", "curation_compliance_agent"],
        },
        "stages": [
            {
                "stage": "ingestion_translation", "agent": "ingestion_translation_agent",
                "agent_input": {"task": "connect_portals_and_ingest_candidate_dataset", "portal_source_manifest": PORTAL_MANIFEST,
                                "candidate_dataset_ids": ["GSE301973"]},
                "input_entities": [], "input_raw": ["00_raw/_corpus/txt/agent_inputs/curation_decisions/CUR-EXCLUDE-GSE301973.txt"],
                "output_entities": [],
                "expected_output": {"normalized_count": 0, "flagged_candidate": "GSE301973", "note": "metadata only — deferred to curation"},
                "decision": "defer_to_curation", "gate": None,
            },
            {
                "stage": "metadata_linking", "agent": "metadata_linking_agent",
                "agent_input": {"task": "link_admitted_entities", "admitted_entities": []},
                "input_entities": [],
                "output_entities": [],
                "expected_output": {"link_count": 0, "reason": "candidate_not_admitted"},
                "decision": "no_action", "gate": None,
            },
            {
                "stage": "search_chat", "agent": "search_chat_agent",
                "agent_input": {"task": "answer_grounded_query", "query": "Summarize the GSE301973 dataset.", "retrieval_scope_entities": []},
                "input_entities": [],
                "output_entities": [],
                "expected_output": {"decision": "refuse_no_grounded_evidence", "reason": "candidate_excluded_patient_derived"},
                "decision": "refuse_no_grounded_evidence", "gate": None,
            },
            {
                "stage": "curation_compliance", "agent": "curation_compliance_agent",
                "agent_input": {"task": "flag_sensitive_content_and_capture_decisions",
                                "records_under_review": ["GSE301973"], "enforced_policy_refs": ["HLS-GEO-400", "HLS-DATA-100"]},
                "input_entities": ["CUR-EXCLUDE-GSE301973"],
                "output_entities": ["CUR-EXCLUDE-GSE301973"],
                "expected_output": {"decision": "deny", "reason": "patient_derived_specimens", "required_human_review": True},
                "decision": "deny", "gate": "denied",
            },
        ],
    },
]


def scenario_folder(scenario: dict) -> str:
    """e.g. 'RKM-001_full_approval' — mirrors loan's APP-XXX_<reason>."""
    return f"{scenario['scenario_id']}_{scenario['path']}"


def stage_folder(stage: dict) -> str:
    return STAGE_FOLDERS[stage["stage"]]
