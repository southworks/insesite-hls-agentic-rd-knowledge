#!/usr/bin/env python3
"""
Generate normalized JSON entity layers from data-generation/corpus/.

The Raw Layer is the baseline. This script derives compact, traceable JSON
entities for agent demos and evaluation, following the numbered folder
convention used by the existing scenario repositories.
"""

from __future__ import annotations

import csv
import json
import re
import shutil
from pathlib import Path
from typing import Any
from xml.etree import ElementTree as ET

from scenarios import (
    SCENARIOS, DEMO_SEQUENCE, scenario_folder, materialized_stages, stage_folder,
    stage_primary, demo_prefix,
)


def scenario_base_path(scenario: dict) -> str:
    """Where this scenario's folders live under expected-outputs/ (DEMO bundle vs standalone)."""
    sid = scenario["scenario_id"]
    if sid in DEMO_SEQUENCE:
        return f"expected-outputs/DEMO_SCENARIO/{demo_prefix(sid)}-{scenario_folder(scenario)}"
    return f"expected-outputs/{scenario_folder(scenario)}"


SCRIPTS = Path(__file__).resolve().parent
DATA_GEN = SCRIPTS.parent
BASE = DATA_GEN
RAW = DATA_GEN / "corpus"
CATALOG_PATH = DATA_GEN / "source" / "_source" / "source_catalog.json"
CATALOG = DATA_GEN / "entity-catalog"

FOLDERS = {
    "research_documents": CATALOG / "01_research_documents",
    "clinical_trials": CATALOG / "02_clinical_trials",
    "experimental_datasets": CATALOG / "03_experimental_datasets",
    "regulatory_submissions": CATALOG / "04_regulatory_submissions",
    "compounds_targets": CATALOG / "05_compounds_targets",
    "evidence_links": CATALOG / "06_evidence_links",
    "curation_decisions": CATALOG / "07_curation_decisions",
    "policy_rag": CATALOG / "08_policy_rag",
    "decision_ground_truth": DATA_GEN / "ground-truth" / "09_decision_ground_truth",
}

# Demo-relevant entity allow-lists. The dataset is trimmed to ONLY the entities the demo's
# data-consuming agents actually use (noise reduction); everything else stays out of the catalog.
KEEP_TRIALS = {"NCT02296125", "NCT02151981"}            # FLAURA, AURA3 (the linked/cited trials)
KEEP_SYNTHETIC_LIMS = {"SYN-LIMS-001", "SYN-LIMS-010"}  # the two synthetic samples used by ING-003


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=True, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"  {path.relative_to(BASE)}")


def write_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content.rstrip() + "\n", encoding="utf-8")
    print(f"  {path.relative_to(BASE)}")


def reset_output() -> None:
    for folder in FOLDERS.values():
        if folder.exists():
            shutil.rmtree(folder)
        folder.mkdir(parents=True, exist_ok=True)


def rel(path: Path) -> str:
    return str(path.relative_to(BASE))


def raw_path(fmt: str, *parts: str | Path) -> Path:
    path = RAW / fmt
    for part in parts:
        path /= part
    return path


def normalize_raw_ref(ref: str) -> str:
    """Point a raw reference at the canonical `_corpus/` layout.

    Some legacy `source_record.json` files still carry pre-`_corpus` paths like
    `00_raw/pdf/...`. The canonical corpus lives under `00_raw/_corpus/`, so rewrite
    any `00_raw/<fmt>/` that is missing the `_corpus` segment.
    """
    if ref.startswith("00_raw/") and not ref.startswith("00_raw/_corpus/"):
        return ref.replace("00_raw/", "00_raw/_corpus/", 1)
    return ref


def compact_text(value: str | None, limit: int = 1200) -> str:
    if not value:
        return ""
    text = " ".join(value.split())
    return text if len(text) <= limit else text[: limit - 3].rstrip() + "..."


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def xml_text(elem: ET.Element | None) -> str:
    if elem is None:
        return ""
    return " ".join("".join(elem.itertext()).split())


def first_xml(root: ET.Element, name: str) -> ET.Element | None:
    for elem in root.iter():
        if local_name(elem.tag) == name:
            return elem
    return None


def all_xml(root: ET.Element, name: str) -> list[ET.Element]:
    return [elem for elem in root.iter() if local_name(elem.tag) == name]


def parse_pmc_article(pmcid: str) -> dict[str, Any]:
    article_json_dir = raw_path("json", "articles", "pmc_oa", pmcid)
    article_xml_dir = raw_path("xml", "articles", "pmc_oa", pmcid)
    metadata = load_json(article_json_dir / "europe_pmc_metadata.json")["resultList"]["result"][0]
    source_record = load_json(article_json_dir / "source_record.json")
    root = ET.parse(article_xml_dir / "article.xml").getroot()

    abstract = compact_text(xml_text(first_xml(root, "abstract")), 1600)
    keywords = [xml_text(elem) for elem in all_xml(root, "kwd") if xml_text(elem)]

    return {
        "document_id": f"RDOC-{pmcid}",
        "document_type": "research_document",
        "source_system": "pmc_open_access",
        "source_identifiers": {
            "pmcid": metadata.get("pmcid", pmcid),
            "pmid": metadata.get("pmid"),
            "doi": metadata.get("doi"),
        },
        "title": metadata.get("title") or xml_text(first_xml(root, "article-title")),
        "journal": metadata.get("journalTitle"),
        "publication_date": metadata.get("firstPublicationDate"),
        "authors": metadata.get("authorString"),
        "license": source_record.get("license"),
        "abstract": abstract,
        "keywords": keywords,
        "therapeutic_area": "EGFR-mutated non-small cell lung cancer",
        "primary_entities": ["osimertinib", "EGFR", "NSCLC"],
        "raw_sources": [
            rel(article_xml_dir / "article.xml"),
            rel(article_json_dir / "europe_pmc_metadata.json"),
            rel(article_xml_dir / "pmc_oa_license.xml"),
        ],
        "provenance": "derived_from_raw_layer",
        "privacy_posture": "no patient-level source selected for normalized entity",
    }


def generate_research_documents(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    docs = []
    for item in catalog["pmc_open_access_articles"]:
        doc = parse_pmc_article(item["pmcid"])
        docs.append(doc)
        write_json(FOLDERS["research_documents"] / f"{doc['document_id']}.json", doc)
    return docs


def generate_clinical_trials(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    trials = []
    for item in catalog["clinical_trials"]:
        nct_id = item["nct_id"]
        if nct_id not in KEEP_TRIALS:
            continue
        trial_json_dir = raw_path("json", "trials", "clinicaltrials_gov", nct_id)
        study = load_json(trial_json_dir / "study.json")
        source_record = load_json(trial_json_dir / "source_record.json")
        protocol = study.get("protocolSection", {})
        identification = protocol.get("identificationModule", {})
        status = protocol.get("statusModule", {})
        sponsors = protocol.get("sponsorCollaboratorsModule", {})
        design = protocol.get("designModule", {})
        arms = protocol.get("armsInterventionsModule", {})
        outcomes = protocol.get("outcomesModule", {})
        eligibility = protocol.get("eligibilityModule", {})

        doc = {
            "trial_id": nct_id,
            "document_type": "clinical_trial",
            "source_system": "clinicaltrials_gov",
            "acronym": identification.get("acronym"),
            "brief_title": identification.get("briefTitle"),
            "official_title": identification.get("officialTitle"),
            "organization": identification.get("organization", {}).get("fullName"),
            "overall_status": status.get("overallStatus"),
            "start_date": status.get("startDateStruct", {}).get("date"),
            "primary_completion_date": status.get("primaryCompletionDateStruct", {}).get("date"),
            "completion_date": status.get("completionDateStruct", {}).get("date"),
            "last_update_posted": status.get("lastUpdatePostDateStruct", {}).get("date"),
            "lead_sponsor": sponsors.get("leadSponsor", {}).get("name"),
            "collaborators": [c.get("name") for c in sponsors.get("collaborators", [])],
            "study_type": design.get("studyType"),
            "phases": design.get("phases", []),
            "enrollment": design.get("enrollmentInfo", {}),
            "conditions": protocol.get("conditionsModule", {}).get("conditions", []),
            "keywords": protocol.get("conditionsModule", {}).get("keywords", []),
            "arms": [
                {
                    "label": a.get("label"),
                    "type": a.get("type"),
                    "description": compact_text(a.get("description"), 600),
                    "interventions": a.get("interventionNames", []),
                }
                for a in arms.get("armGroups", [])
            ],
            "interventions": [
                {
                    "name": i.get("name"),
                    "type": i.get("type"),
                    "arm_group_labels": i.get("armGroupLabels", []),
                    "other_names": i.get("otherNames", []),
                }
                for i in arms.get("interventions", [])
            ],
            "primary_outcomes": [
                {
                    "measure": o.get("measure"),
                    "time_frame": o.get("timeFrame"),
                    "description": compact_text(o.get("description"), 600),
                }
                for o in outcomes.get("primaryOutcomes", [])
            ],
            "secondary_outcomes": [
                {
                    "measure": o.get("measure"),
                    "time_frame": o.get("timeFrame"),
                }
                for o in outcomes.get("secondaryOutcomes", [])[:12]
            ],
            "eligibility_summary": {
                "sex": eligibility.get("sex"),
                "minimum_age": eligibility.get("minimumAge"),
                "maximum_age": eligibility.get("maximumAge"),
                "healthy_volunteers": eligibility.get("healthyVolunteers"),
            },
            "has_results": study.get("hasResults"),
            "public_documents": [
                {**d, "local_path": normalize_raw_ref(d["local_path"])}
                for d in source_record.get("documents", [])
            ],
            "raw_sources": [rel(trial_json_dir / "study.json")] + [
                normalize_raw_ref(d["local_path"]) for d in source_record.get("documents", [])
            ],
            "provenance": "derived_from_raw_layer",
            "privacy_posture": "aggregate clinical trial registration/results only",
        }
        trials.append(doc)
        write_json(FOLDERS["clinical_trials"] / f"TRIAL-{nct_id}.json", doc)
    return trials


def generate_experimental_datasets(catalog: dict[str, Any]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    datasets = []
    samples = []

    for item in catalog["geo_cell_line_datasets"]:
        accession = item["accession"]
        ds_json_dir = raw_path("json", "datasets", "geo", accession)
        ds_txt_dir = raw_path("txt", "datasets", "geo", accession)
        summary = load_json(ds_json_dir / "geo_esummary.json")
        uid = summary["result"]["uids"][0]
        record = summary["result"][uid]

        sample_list = record.get("samples", [])
        dataset_doc = {
            "dataset_id": accession,
            "document_type": "experimental_dataset",
            "source_system": "ncbi_geo",
            "title": record.get("title"),
            "taxon": record.get("taxon"),
            "summary": compact_text(record.get("summary"), 1400),
            "overall_design": compact_text(record.get("overallDesign"), 1000),
            "sample_count": len(sample_list),
            "sample_accessions": [s.get("accession") for s in sample_list],
            "selected_reason": item["role"],
            "assay_context": infer_assay_context(record.get("title", ""), record.get("summary", "")),
            "raw_sources": [
                rel(ds_json_dir / "geo_esummary.json"),
                rel(ds_txt_dir / "series_soft.txt"),
                rel(ds_json_dir / "source_record.json"),
            ],
            "provenance": "derived_from_raw_layer",
            "privacy_posture": "cell-line or assay-level source selected",
        }
        datasets.append(dataset_doc)
        write_json(FOLDERS["experimental_datasets"] / f"DATASET-{accession}.json", dataset_doc)

    # Per-sample GEO entities (SAMPLE-GSM*) are deliberately NOT materialized — the demo links at
    # the dataset (series) level, so the individual assay samples would be pure noise.

    lims_path = raw_path("csv", "synthetic_eln_lims", "lims_sample_manifest.csv")
    with lims_path.open(encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            if row["SAMPLE_ID"] not in KEEP_SYNTHETIC_LIMS:
                continue
            sample_doc = {
                "sample_id": row["SAMPLE_ID"],
                "document_type": "synthetic_lims_sample",
                "source_system": "synthetic_lims",
                "source_geo_series": row["SOURCE_GEO_SERIES"],
                "source_geo_sample": row["SOURCE_GEO_SAMPLE"],
                "model": row["MODEL"],
                "condition": row["CONDITION"],
                "timepoint": row["TIMEPOINT"],
                "replicate": int(row["REPLICATE"]),
                "assay": row["ASSAY"],
                "intended_signal": row["INTENDED_SIGNAL"],
                "raw_sources": [rel(lims_path)],
                "provenance": "synthetic_from_public_structure",
                "privacy_posture": "fictional/synthetic operational record; no patient data",
            }
            samples.append(sample_doc)
            write_json(FOLDERS["experimental_datasets"] / f"{sample_doc['sample_id']}.json", sample_doc)

    return datasets, samples


def infer_model(title: str) -> str:
    upper = title.upper()
    if "H1650" in upper:
        return "H1650"
    if "PC9OR" in upper:
        return "PC9OR"
    if "PC9-CAS9" in upper:
        return "PC9-Cas9"
    if "PC9" in upper:
        return "PC9"
    return "unknown"


def infer_condition(title: str) -> str:
    lower = title.lower()
    if "osimertinib" in lower:
        return "osimertinib"
    if "erlotinib" in lower:
        return "erlotinib"
    if "dmso" in lower:
        return "DMSO"
    if "vehicle" in lower:
        return "vehicle"
    if "resistant" in lower:
        return "resistant"
    if "sensitive" in lower:
        return "sensitive"
    if "untreated" in lower:
        return "untreated"
    return "not_specified"


def infer_assay_context(title: str, summary: str) -> list[str]:
    text = f"{title} {summary}".lower()
    contexts = []
    for keyword, label in [
        ("rna", "RNA-seq"),
        ("crispr", "CRISPR/dependency screen"),
        ("resistance", "resistance model"),
        ("chromatin", "chromatin profiling"),
        ("egfr", "EGFR inhibition"),
        ("osimertinib", "osimertinib response"),
    ]:
        if keyword in text:
            contexts.append(label)
    return contexts


def generate_compounds_targets(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    out = []
    chembl_dir = raw_path("json", "registries", "chembl", "CHEMBL3353410")
    compound = load_json(chembl_dir / "molecule.json")
    mechanism = load_json(chembl_dir / "mechanism.json")
    target = load_json(chembl_dir / "target_CHEMBL203.json")
    first_mechanism = (mechanism.get("mechanisms") or [{}])[0]

    compound_doc = {
        "entity_id": "CMP-CHEMBL3353410",
        "document_type": "compound",
        "preferred_name": compound.get("pref_name"),
        "aliases": catalog["compound_anchor"]["aliases"],
        "chembl_id": compound.get("molecule_chembl_id"),
        "molecule_type": compound.get("molecule_type"),
        "max_phase": compound.get("max_phase"),
        "first_approval": compound.get("first_approval"),
        "atc_classifications": compound.get("atc_classifications", []),
        "mechanism_of_action": first_mechanism.get("mechanism_of_action"),
        "action_type": first_mechanism.get("action_type"),
        "target_chembl_id": first_mechanism.get("target_chembl_id"),
        "raw_sources": [
            rel(chembl_dir / "molecule.json"),
            rel(chembl_dir / "mechanism.json"),
        ],
        "provenance": "derived_from_raw_layer",
    }
    out.append(compound_doc)
    write_json(FOLDERS["compounds_targets"] / "CMP-CHEMBL3353410.json", compound_doc)

    target_doc = {
        "entity_id": "TGT-CHEMBL203",
        "document_type": "molecular_target",
        "preferred_name": target.get("pref_name"),
        "gene_symbol": "EGFR",
        "aliases": ["ERBB1", "Epidermal growth factor receptor"],
        "chembl_id": target.get("target_chembl_id"),
        "organism": target.get("organism"),
        "target_type": target.get("target_type"),
        "linked_compounds": ["CMP-CHEMBL3353410"],
        "raw_sources": [rel(chembl_dir / "target_CHEMBL203.json")],
        "provenance": "derived_from_raw_layer",
    }
    out.append(target_doc)
    write_json(FOLDERS["compounds_targets"] / "TGT-CHEMBL203.json", target_doc)

    # Individual biomarker entities (BMK-*) are not materialized — the demo's metadata & linking
    # agent extracts the compound + target, not the biomarker catalog, so they would be noise.
    return out


def generate_regulatory_submissions() -> list[dict[str, Any]]:
    out = []
    label_path = raw_path("json", "regulatory", "OPENFDA_LABEL_TAGRISSO", "OPENFDA_LABEL_TAGRISSO.json")
    drugsfda_path = raw_path("json", "regulatory", "OPENFDA_DRUGSFDA_NDA208065", "OPENFDA_DRUGSFDA_NDA208065.json")
    label = load_json(label_path)["results"][0]
    drugsfda = load_json(drugsfda_path)["results"][0]
    openfda = label.get("openfda", {})

    app_doc = {
        "document_id": "REG-NDA208065",
        "document_type": "regulatory_application",
        "source_system": "openfda_drugsfda",
        "application_number": drugsfda.get("application_number"),
        "sponsor_name": drugsfda.get("sponsor_name"),
        "brand_name": first(openfda.get("brand_name")),
        "generic_name": first(openfda.get("generic_name")),
        "products": drugsfda.get("products", []),
        "submissions": drugsfda.get("submissions", []),
        "priority_submission_count": sum(1 for s in drugsfda.get("submissions", []) if s.get("review_priority") == "PRIORITY"),
        "raw_sources": [rel(drugsfda_path)],
        "provenance": "derived_from_raw_layer",
    }
    out.append(app_doc)
    write_json(FOLDERS["regulatory_submissions"] / "REG-NDA208065.json", app_doc)

    label_doc = {
        "document_id": "LBL-TAGRISSO-OPENFDA",
        "document_type": "product_label",
        "source_system": "openfda_label",
        "application_number": first(openfda.get("application_number")),
        "brand_name": first(openfda.get("brand_name")),
        "generic_name": first(openfda.get("generic_name")),
        "manufacturer": first(openfda.get("manufacturer_name")),
        "effective_time": label.get("effective_time"),
        "route": openfda.get("route", []),
        "pharmacologic_classes": openfda.get("pharm_class_epc", []) + openfda.get("pharm_class_moa", []),
        "indications_and_usage": compact_text(first(label.get("indications_and_usage")), 1400),
        "dosage_and_administration": compact_text(first(label.get("dosage_and_administration")), 900),
        "warnings_and_precautions": compact_text(first(label.get("warnings_and_cautions")) or first(label.get("warnings")), 1200),
        "raw_sources": [rel(label_path)],
        "provenance": "derived_from_raw_layer",
    }
    out.append(label_doc)
    write_json(FOLDERS["regulatory_submissions"] / "LBL-TAGRISSO-OPENFDA.json", label_doc)

    # Regulatory source documents (REGDOC-*: approval letters, reviews, EPAR) are not materialized —
    # the demo links the application + label only, so the extra source docs would be noise.
    return out


def first(value: Any) -> Any:
    if isinstance(value, list):
        return value[0] if value else None
    return value


def infer_regulatory_system(source_id: str) -> str:
    if source_id.startswith("EMA"):
        return "ema"
    if source_id.startswith("DRUGSATFDA"):
        return "drugsatfda"
    if source_id.startswith("OPENFDA"):
        return "openfda"
    return "regulatory_public_source"


def generate_policy_rag() -> list[dict[str, Any]]:
    policies = [
        {
            "policy_id": "HLS-DATA-100",
            "policy_ref": "HLS-DATA-100",
            "title": "Patient-level data exclusion",
            "rule": "Do not ingest patient-level records, case report narratives, or real patient trajectories into this dataset seed.",
            "threshold": "Any source that contains real patient identifiers or patient specimen labels is excluded.",
            "action": "deny_source_for_raw_layer",
            "source_refs": ["FDA_CLINICAL_TRIALS_HUMAN_SUBJECT_PROTECTION", "EU_CLINICAL_TRIALS_REGULATION_536_2014"],
        },
        {
            "policy_id": "HLS-DATA-110",
            "policy_ref": "HLS-DATA-110",
            "title": "Synthetic ELN/LIMS labeling",
            "rule": "Generated ELN, LIMS, reviewer, scientist, or repository records must be explicitly labeled synthetic.",
            "threshold": "All generated lab operations records must include provenance synthetic_from_public_structure.",
            "action": "require_synthetic_provenance",
            "source_refs": ["AACT_HOME", "CLINICALTRIALS_GOV_DATA_API"],
        },
        {
            "policy_id": "HLS-LIC-200",
            "policy_ref": "HLS-LIC-200",
            "title": "Open-access full-text license check",
            "rule": "Store article full text only when PMC OA license metadata confirms acceptable reuse terms.",
            "threshold": "PMC OA API license must match the catalog expected_license before article.xml is kept.",
            "action": "allow_full_text_when_license_verified",
            "source_refs": ["PMC Open Access", "Europe PMC"],
        },
        {
            "policy_id": "HLS-TRIAL-300",
            "policy_ref": "HLS-TRIAL-300",
            "title": "Aggregate trial record use",
            "rule": "ClinicalTrials.gov records may be used only as aggregate public registry/results documents.",
            "threshold": "Use protocol, SAP, eligibility, arms, outcomes, and aggregate results; do not generate patient-level rows.",
            "action": "allow_aggregate_trial_documents",
            "source_refs": ["CLINICALTRIALS_GOV_DATA_API", "AACT_HOME"],
        },
        {
            "policy_id": "HLS-GEO-400",
            "policy_ref": "HLS-GEO-400",
            "title": "Cell-line dataset preference",
            "rule": "Use GEO records whose samples are cell-line or assay-level records for the first dataset iteration.",
            "threshold": "Reject records whose sample titles reference patient FFPE, before/after treatment patient specimens, or similar patient-derived labels.",
            "action": "allow_cell_line_dataset_reject_patient_sample_dataset",
            "source_refs": ["NCBI GEO", "source_catalog_exclusions"],
        },
        {
            "policy_id": "HLS-REG-500",
            "policy_ref": "HLS-REG-500",
            "title": "Public regulatory source use",
            "rule": "Use only public regulatory labels, approval letters, reviews, and EPAR pages for regulatory entities.",
            "threshold": "Source must be a public FDA/openFDA/Drugs@FDA/EMA document or API response.",
            "action": "allow_public_regulatory_documents",
            "source_refs": ["OPENFDA_LABEL_TAGRISSO", "OPENFDA_DRUGSFDA_NDA208065", "EMA_TAGRISSO_EPAR"],
        },
    ]

    source_map = policy_source_map()
    for doc in policies:
        doc.update(
            {
                "document_type": "policy_rag_rule",
                "source_system": "dataset_policy",
                "raw_sources": [source_map[s] for s in doc["source_refs"] if s in source_map],
                "provenance": "curated_from_raw_layer_policy_sources",
            }
        )
        write_json(FOLDERS["policy_rag"] / f"{doc['policy_id']}.json", doc)
    return policies


def policy_source_map() -> dict[str, str]:
    mapping = {
        "source_catalog_exclusions": rel(CATALOG_PATH),
        "PMC Open Access": rel(RAW / "raw_manifest.json"),
        "Europe PMC": rel(RAW / "raw_manifest.json"),
        "NCBI GEO": rel(RAW / "raw_manifest.json"),
    }
    for path in raw_path("json", "policies").glob("*/source_record.json"):
        source = load_json(path)
        mapping[source["source_id"]] = rel(path)
    for path in raw_path("json", "regulatory").glob("*/source_record.json"):
        source = load_json(path)
        mapping[source["source_id"]] = rel(path)
    return mapping


def generate_evidence_links(
    research_docs: list[dict[str, Any]],
    trials: list[dict[str, Any]],
    datasets: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    links = []

    def add(link_id: str, source: str, target: str, relation: str, rationale: str, refs: list[str]) -> None:
        doc = {
            "link_id": link_id,
            "document_type": "evidence_link",
            "source_entity_id": source,
            "target_entity_id": target,
            "relation_type": relation,
            "rationale": rationale,
            "evidence_strength": "curated_baseline",
            "raw_sources": refs,
            "provenance": "curated_from_raw_layer_evidence",
        }
        links.append(doc)
        write_json(FOLDERS["evidence_links"] / f"{link_id}.json", doc)

    # Only the two links the metadata & linking agent produces in the demo are materialized
    # (FLAURA -> NDA208065 -> Tagrisso label). Per-trial/-article/-dataset links are noise here.
    add(
        "LINK-REG-NDA208065-LBL-TAGRISSO-OPENFDA",
        "REG-NDA208065",
        "LBL-TAGRISSO-OPENFDA",
        "has_public_label",
        "openFDA application and label records share NDA208065/Tagrisso identifiers.",
        [rel(raw_path("json", "regulatory", "OPENFDA_DRUGSFDA_NDA208065", "OPENFDA_DRUGSFDA_NDA208065.json"))],
    )
    add(
        "LINK-FLAURA-REG-NDA208065",
        "TRIAL-NCT02296125",
        "REG-NDA208065",
        "supports_regulatory_evidence_context",
        "FLAURA is the core first-line trial context for the selected osimertinib regulatory baseline.",
        [rel(raw_path("json", "trials", "clinicaltrials_gov", "NCT02296125", "study.json"))],
    )
    return links


def generate_curation_decisions(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    decisions = []

    def add(decision_id: str, source_id: str, source_type: str, decision: str, reason: str, policy_refs: list[str], raw_sources: list[str]) -> None:
        doc = {
            "decision_id": decision_id,
            "document_type": "curation_decision",
            "source_id": source_id,
            "source_type": source_type,
            "decision": decision,
            "reason": reason,
            "required_human_review": decision == "require_review",
            "policy_refs": policy_refs,
            "curator": "Marisol Vega, Research Data Steward (fictional)",
            "decision_date": "2026-06-17",
            "raw_sources": raw_sources,
            "provenance": "curated_from_raw_layer_evidence",
        }
        decisions.append(doc)
        write_json(FOLDERS["curation_decisions"] / f"{decision_id}.json", doc)

    # Only the deny/exclude decisions are materialized — they are the guardrail evidence the
    # ING-002/ING-004 scenarios reference. Per-source "approve" decisions are implicit (the entity
    # exists) and would be noise.
    for excluded in catalog["excluded_sources"]:
        add(
            f"CUR-EXCLUDE-{excluded['source_id']}",
            excluded["source_id"],
            "excluded_candidate",
            "deny",
            excluded["reason"],
            ["HLS-DATA-100", "HLS-GEO-400" if excluded["source_id"].startswith("GSE") else "HLS-LIC-200"],
            [rel(CATALOG_PATH)],
        )
    return decisions


def generate_ground_truth() -> list[dict[str, Any]]:
    """Emit one e2e ground-truth rollup per scenario (ING-XXX.json / QRY-XXX.json).

    The rollup is the full **answer key**: the controlled UI trigger and EVERY ordered stage the
    demo traverses — including the agents we materialize datasets for AND the memory stages we do
    not (the human-approval gates and persistence). Only the data-consuming agents + the response
    output carry a `raw_layer_folder`; gate/sink stages are `materialized: false` and live here only.
    The scenario set is defined once in `scenarios.py`.
    """
    gt_dir = FOLDERS["decision_ground_truth"]
    # Drop legacy/previous cases so the folder holds only the current rollups + SCHEMA.md.
    for pat in ("GT-*.json", "RKM-*.json", "ING-*.json", "QRY-*.json"):
        for stale in gt_dir.glob(pat):
            stale.unlink()

    rollups: list[dict[str, Any]] = []
    for scenario in SCENARIOS:
        base = scenario_base_path(scenario)
        materialized = materialized_stages(scenario)
        stages = []
        for order, stage in enumerate(scenario["stages"], start=1):
            _filename, key, content = stage_primary(stage)
            is_materialized = stage in materialized
            entry = {
                "order": order,
                "stage": stage["stage"],
                "kind": stage["kind"],
                "agent": stage.get("agent"),
                "actor": stage.get("actor"),
                "materialized": is_materialized,
                "raw_layer_folder": f"{base}/{stage_folder(scenario, stage)}/" if is_materialized else None,
                key: content,
                "input_entities": stage.get("input_entities", []),
                "output_entities": stage.get("output_entities", []),
                "expected_output": stage.get("expected_output"),
                "decision": stage.get("decision"),
                "gate": stage.get("gate"),
            }
            stages.append(entry)
        rollup = {
            "scenario_id": scenario["scenario_id"],
            "document_type": "decision_ground_truth",
            "scenario_kind": "e2e_phase_path",
            "flow": scenario["flow"],
            "phase": scenario["phase"],
            "document_date": "2026-06-24",
            "source_system": "hls_knowledge_mining_ground_truth",
            "title": scenario["title"],
            "path": scenario["path"],
            "scenario_folder": base,
            "trigger": scenario["trigger"],
            "stages": stages,
            "final_outcome": scenario["final_outcome"],
            "required_human_review": scenario["required_human_review"],
            "raw_sources": [rel(RAW / "raw_manifest.json")],
        }
        if "kb_state" in scenario:
            rollup["kb_state"] = scenario["kb_state"]
        rollups.append(rollup)
        write_json(gt_dir / f"{scenario['scenario_id']}.json", rollup)

    return rollups


def write_schemas() -> None:
    schemas = {
        "research_documents": """# 01 Research Documents Schema

Normalized research article entities derived from `00_raw/_corpus/xml/articles/pmc_oa/`
and `00_raw/_corpus/json/articles/pmc_oa/`.

## Required fields

- `document_id`
- `document_type` = `research_document`
- `source_system`
- `source_identifiers.pmcid`
- `title`
- `journal`
- `publication_date`
- `license`
- `abstract`
- `primary_entities`
- `raw_sources`
- `provenance`
- `privacy_posture`
""",
        "clinical_trials": """# 02 Clinical Trials Schema

Clinical trial entities derived from ClinicalTrials.gov study JSON and protocol/SAP PDFs.

## Required fields

- `trial_id`
- `document_type` = `clinical_trial`
- `source_system`
- `acronym`
- `brief_title`
- `overall_status`
- `lead_sponsor`
- `phases`
- `enrollment`
- `conditions`
- `arms`
- `interventions`
- `primary_outcomes`
- `has_results`
- `public_documents`
- `raw_sources`
- `privacy_posture`
""",
        "experimental_datasets": """# 03 Experimental Datasets Schema

Experimental dataset entities derived from GEO, plus the synthetic LIMS samples used by the
synthetic-provenance scenario. Per-sample GEO entities (`SAMPLE-GSM*`) are intentionally not
materialized — the demo links at the dataset (series) level.

## Entity types

- `experimental_dataset`
- `synthetic_lims_sample`

## Required fields

- dataset entities: `dataset_id`, `title`, `taxon`, `sample_count`, `sample_accessions`, `raw_sources`
- synthetic sample entities: `sample_id`, `source_geo_series`, `model`, `condition`, `raw_sources`
- all entities: `document_type`, `source_system`, `provenance`, `privacy_posture`
""",
        "regulatory_submissions": """# 04 Regulatory Submissions Schema

Regulatory application and product-label entities derived from openFDA and Drugs@FDA raw files.
Source documents (`REGDOC-*`: approval letters, reviews, EPAR) are not materialized — the demo
links the application + label only.

## Entity types

- `regulatory_application`
- `product_label`

## Required fields

- `document_id`
- `document_type`
- `source_system`
- `application_number` where applicable
- `raw_sources`
- `provenance`
""",
        "compounds_targets": """# 05 Compounds and Targets Schema

Compound and molecular-target entities derived from ChEMBL — the entities the metadata & linking
agent extracts from the ingested evidence. Biomarker entities (`BMK-*`) are not materialized.

## Entity types

- `compound`
- `molecular_target`

## Required fields

- `entity_id`
- `document_type`
- `preferred_name`
- `linked_compounds` / `target_chembl_id` where applicable
- `raw_sources`
- `provenance`
""",
        "policy_rag": """# 08 Policy RAG Schema

Policy rule JSON entities used by retrieval and compliance agents.

## Required fields

- `policy_id`
- `policy_ref`
- `document_type` = `policy_rag_rule`
- `title`
- `rule`
- `threshold`
- `action`
- `source_refs`
- `raw_sources`
- `provenance`
""",
        "evidence_links": """# 06 Evidence Links Schema

Curated links between normalized entities, each backed by Raw Layer evidence.

## Required fields

- `link_id`
- `document_type` = `evidence_link`
- `source_entity_id`
- `target_entity_id`
- `relation_type`
- `rationale`
- `evidence_strength`
- `raw_sources`
- `provenance`
""",
        "curation_decisions": """# 07 Curation Decisions Schema

Deny/exclude decisions for source inclusion and compliance guardrails (the guardrail evidence the
ING-002/ING-004 scenarios reference). Per-source "approve" decisions are implicit.

## Required fields

- `decision_id`
- `document_type` = `curation_decision`
- `source_id`
- `source_type`
- `decision`
- `reason`
- `required_human_review`
- `policy_refs`
- `curator`
- `decision_date`
- `raw_sources`
- `provenance`
""",
        "decision_ground_truth": """# 09 Decision Ground Truth Schema

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
""",
    }

    for key, content in schemas.items():
        write_text(FOLDERS[key] / "SCHEMA.md", content)


def write_dataset_manifest(counts: dict[str, int]) -> None:
    manifest = {
        "dataset_id": "hls-agentic-rd-knowledge-mining",
        "scenario": "HLS Agentic R&D knowledge mining with Cohere models",
        "therapeutic_area": "EGFR-mutated non-small cell lung cancer",
        "compound_anchor": "osimertinib / Tagrisso / AZD9291",
        "raw_layer_manifest": rel(RAW / "raw_manifest.json"),
        "normalized_layers": [
            {"folder": "01_research_documents", "entity_count": counts["research_documents"]},
            {"folder": "02_clinical_trials", "entity_count": counts["clinical_trials"]},
            {"folder": "03_experimental_datasets", "entity_count": counts["experimental_datasets"]},
            {"folder": "04_regulatory_submissions", "entity_count": counts["regulatory_submissions"]},
            {"folder": "05_compounds_targets", "entity_count": counts["compounds_targets"]},
            {"folder": "06_evidence_links", "entity_count": counts["evidence_links"]},
            {"folder": "07_curation_decisions", "entity_count": counts["curation_decisions"]},
            {"folder": "08_policy_rag", "entity_count": counts["policy_rag"]},
            {"folder": "09_decision_ground_truth", "entity_count": counts["decision_ground_truth"]},
        ],
        "privacy_posture": {
            "patient_level_data": "excluded",
            "patient_names": "excluded",
            "synthetic_people": "fictional lab/reviewer names only",
            "selected_geo_records": "cell-line or assay-level records only",
        },
    }
    write_json(DATA_GEN / "scripts" / "dataset-manifest.json", manifest)


def main() -> int:
    catalog = load_json(CATALOG_PATH)
    if not RAW.exists():
        raise RuntimeError("Raw layer is missing. Run generate_raw_layer.py first.")

    reset_output()
    research_docs = generate_research_documents(catalog)
    trials = generate_clinical_trials(catalog)
    datasets, samples = generate_experimental_datasets(catalog)
    compounds_targets = generate_compounds_targets(catalog)
    regulatory = generate_regulatory_submissions()
    policies = generate_policy_rag()
    links = generate_evidence_links(research_docs, trials, datasets)
    decisions = generate_curation_decisions(catalog)
    ground_truth = generate_ground_truth()
    write_schemas()

    counts = {
        "research_documents": len(research_docs),
        "clinical_trials": len(trials),
        "experimental_datasets": len(datasets) + len(samples),
        "compounds_targets": len(compounds_targets),
        "regulatory_submissions": len(regulatory),
        "policy_rag": len(policies),
        "evidence_links": len(links),
        "curation_decisions": len(decisions),
        "decision_ground_truth": len(ground_truth),
    }
    write_dataset_manifest(counts)
    print("\nNormalized layers generated successfully.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
