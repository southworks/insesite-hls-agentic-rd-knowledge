#!/usr/bin/env python3
"""
Generate normalized JSON entity layers from dataset-seed/00_raw/.

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


BASE = Path(__file__).resolve().parent
RAW = BASE / "00_raw" / "_corpus"  # canonical corpus (see generate_raw_layer.py)
CATALOG_PATH = BASE / "_source" / "source_catalog.json"

FOLDERS = {
    "research_documents": BASE / "01_research_documents",
    "clinical_trials": BASE / "02_clinical_trials",
    "experimental_datasets": BASE / "03_experimental_datasets",
    "biomarkers_targets": BASE / "04_biomarkers_and_targets",
    "regulatory_submissions": BASE / "05_regulatory_submissions",
    "policy_rag": BASE / "06_policy_rag",
    "evidence_links": BASE / "07_evidence_links",
    "curation_decisions": BASE / "08_curation_decisions",
    "decision_ground_truth": BASE / "09_decision_ground_truth",
}


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
            "public_documents": source_record.get("documents", []),
            "raw_sources": [rel(trial_json_dir / "study.json")] + [
                d["local_path"] for d in source_record.get("documents", [])
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

        for sample in sample_list[:20]:
            sample_doc = {
                "sample_id": sample.get("accession"),
                "document_type": "assay_sample",
                "source_system": "ncbi_geo",
                "dataset_id": accession,
                "sample_title": sample.get("title"),
                "model": infer_model(sample.get("title", "")),
                "condition": infer_condition(sample.get("title", "")),
                "raw_sources": [rel(ds_json_dir / "geo_esummary.json")],
                "provenance": "derived_from_raw_layer",
                "privacy_posture": "sample title reviewed for cell-line context",
            }
            samples.append(sample_doc)
            write_json(FOLDERS["experimental_datasets"] / f"SAMPLE-{sample_doc['sample_id']}.json", sample_doc)

    lims_path = raw_path("csv", "synthetic_eln_lims", "lims_sample_manifest.csv")
    with lims_path.open(encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
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


def generate_biomarkers_targets(catalog: dict[str, Any]) -> list[dict[str, Any]]:
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
    write_json(FOLDERS["biomarkers_targets"] / "CMP-CHEMBL3353410.json", compound_doc)

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
    write_json(FOLDERS["biomarkers_targets"] / "TGT-CHEMBL203.json", target_doc)

    biomarkers = [
        ("BMK-EGFR-EX19DEL", "EGFR exon 19 deletion", "sensitizing_mutation"),
        ("BMK-EGFR-L858R", "EGFR L858R", "sensitizing_mutation"),
        ("BMK-EGFR-T790M", "EGFR T790M", "resistance_mutation"),
        ("BMK-EGFR-C797S", "EGFR C797S", "resistance_mutation"),
        ("BMK-FGFR4", "FGFR4 pathway signal", "resistance_pathway"),
        ("BMK-GLMP", "GLMP autophagy/RhoA pathway signal", "resistance_pathway"),
        ("BMK-PKM2", "PKM2 fatty-acid biosynthesis signal", "response_modifier"),
    ]
    for entity_id, name, category in biomarkers:
        doc = {
            "entity_id": entity_id,
            "document_type": "biomarker",
            "name": name,
            "category": category,
            "disease_context": "EGFR-mutated NSCLC",
            "linked_target_id": "TGT-CHEMBL203" if name.startswith("EGFR") else None,
            "linked_compound_ids": ["CMP-CHEMBL3353410"],
            "evidence_source_types": ["clinical_trials", "research_documents", "experimental_datasets"],
            "raw_sources": [rel(RAW / "raw_manifest.json")],
            "provenance": "curated_from_raw_layer_evidence",
        }
        out.append(doc)
        write_json(FOLDERS["biomarkers_targets"] / f"{entity_id}.json", doc)

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

    source_records = [
        raw_path("json", "regulatory", "DRUGSATFDA_NDA208065_APPROVAL_LETTER", "source_record.json"),
        raw_path("json", "regulatory", "DRUGSATFDA_NDA208065_SUMMARY_REVIEW", "source_record.json"),
        raw_path("json", "regulatory", "DRUGSATFDA_NDA208065_OVERVIEW", "source_record.json"),
        raw_path("json", "regulatory", "EMA_TAGRISSO_EPAR", "source_record.json"),
    ]
    for source_record_path in source_records:
        source_record = load_json(source_record_path)
        source_id = source_record["source_id"]
        raw_files = [source_record["local_path"]]
        doc = {
            "document_id": f"REGDOC-{source_id}",
            "document_type": "regulatory_source_document",
            "source_system": infer_regulatory_system(source_id),
            "source_id": source_id,
            "url": source_record.get("url"),
            "application_number": "NDA208065" if "NDA208065" in source_id else None,
            "compound_id": "CMP-CHEMBL3353410",
            "raw_sources": raw_files + [rel(source_record_path)],
            "provenance": "derived_from_raw_layer",
        }
        out.append(doc)
        write_json(FOLDERS["regulatory_submissions"] / f"REGDOC-{source_id}.json", doc)

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

    for trial in trials:
        add(
            f"LINK-{trial['trial_id']}-CMP-CHEMBL3353410",
            f"TRIAL-{trial['trial_id']}",
            "CMP-CHEMBL3353410",
            "evaluates_intervention",
            "Selected trial evaluates osimertinib/AZD9291 or an osimertinib-containing regimen.",
            trial["raw_sources"],
        )

    for doc in research_docs:
        pmcid = doc["source_identifiers"]["pmcid"]
        add(
            f"LINK-{pmcid}-TGT-CHEMBL203",
            doc["document_id"],
            "TGT-CHEMBL203",
            "discusses_target_or_pathway",
            "Research document belongs to the EGFR-mutated NSCLC/osimertinib source baseline.",
            doc["raw_sources"][:2],
        )

    dataset_links = {
        "GSE323366": "LINK-GSE323366-CMP-CHEMBL3353410",
        "GSE323365": "LINK-GSE323365-TGT-CHEMBL203",
        "GSE272182": "LINK-GSE272182-BMK-EGFR-T790M",
        "GSE300311": "LINK-GSE300311-BMK-PKM2",
        "GSE298111": "LINK-GSE298111-BMK-GLMP",
    }
    for dataset in datasets:
        target = {
            "GSE323366": "CMP-CHEMBL3353410",
            "GSE323365": "TGT-CHEMBL203",
            "GSE272182": "BMK-EGFR-T790M",
            "GSE300311": "BMK-PKM2",
            "GSE298111": "BMK-GLMP",
        }[dataset["dataset_id"]]
        add(
            dataset_links[dataset["dataset_id"]],
            f"DATASET-{dataset['dataset_id']}",
            target,
            "provides_experimental_evidence",
            "Dataset was selected for cell-line or assay-level evidence in the osimertinib/EGFR baseline.",
            dataset["raw_sources"],
        )

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

    for article in catalog["pmc_open_access_articles"]:
        add(
            f"CUR-{article['pmcid']}",
            article["pmcid"],
            "pmc_open_access_article",
            "approve",
            "PMC OA license matched expected reuse posture and article is not a case-report source.",
            ["HLS-LIC-200", "HLS-DATA-100"],
            [rel(raw_path("xml", "articles", "pmc_oa", article["pmcid"], "pmc_oa_license.xml"))],
        )

    for dataset in catalog["geo_cell_line_datasets"]:
        add(
            f"CUR-{dataset['accession']}",
            dataset["accession"],
            "geo_dataset",
            "approve",
            "Selected GEO record uses cell-line or assay-level source material.",
            ["HLS-GEO-400", "HLS-DATA-100"],
            [rel(raw_path("txt", "datasets", "geo", dataset["accession"], "series_soft.txt"))],
        )

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

    add(
        "CUR-SYNTHETIC-ELN-LIMS",
        "synthetic_eln_lims",
        "synthetic_operational_record",
        "approve",
        "Synthetic records are clearly labeled and use only fictional staff names.",
        ["HLS-DATA-110"],
        [rel(raw_path("txt", "synthetic_eln_lims", "lims_quality_control_report.txt"))],
    )
    return decisions


def generate_ground_truth() -> list[dict[str, Any]]:
    cases = [
        {
            "scenario_id": "GT-INGEST-ARTICLES",
            "expected_agent": "ingestion_translation_agent",
            "expected_decision": "approve",
            "primary_reason": "all_selected_article_full_text_has_verified_pmc_oa_license",
            "top_policy_refs": ["HLS-LIC-200", "HLS-DATA-100"],
            "source_entities": ["RDOC-PMC6889286", "RDOC-PMC5447962", "RDOC-PMC13070087", "RDOC-PMC13129538", "RDOC-PMC13143971"],
            "expected_outputs": {"accepted_article_count": 5, "denied_article_count": 1},
        },
        {
            "scenario_id": "GT-LINK-TRIAL-REGULATORY",
            "expected_agent": "metadata_linking_agent",
            "expected_decision": "approve",
            "primary_reason": "trial_and_regulatory_entities_link_through_osimertinib_tagrisso_azd9291_identifiers",
            "top_policy_refs": ["HLS-TRIAL-300", "HLS-REG-500"],
            "source_entities": ["TRIAL-NCT02296125", "TRIAL-NCT02151981", "REG-NDA208065", "LBL-TAGRISSO-OPENFDA"],
            "expected_outputs": {"required_links": ["LINK-FLAURA-REG-NDA208065", "LINK-REG-NDA208065-LBL-TAGRISSO-OPENFDA"]},
        },
        {
            "scenario_id": "GT-USE-CELL-LINE-DATASETS",
            "expected_agent": "metadata_linking_agent",
            "expected_decision": "approve",
            "primary_reason": "selected_geo_records_are_cell_line_or_assay_level",
            "top_policy_refs": ["HLS-GEO-400", "HLS-DATA-100"],
            "source_entities": ["DATASET-GSE323366", "DATASET-GSE323365", "DATASET-GSE272182", "DATASET-GSE300311", "DATASET-GSE298111"],
            "expected_outputs": {"accepted_geo_count": 5, "excluded_geo_count": 2},
        },
        {
            "scenario_id": "GT-REQUIRE-SYNTHETIC-PROVENANCE",
            "expected_agent": "curation_compliance_agent",
            "expected_decision": "approve_with_required_labeling",
            "primary_reason": "eln_lims_records_are_synthetic_and_marked_synthetic_from_public_structure",
            "top_policy_refs": ["HLS-DATA-110"],
            "source_entities": ["SYN-LIMS-001", "SYN-LIMS-010"],
            "expected_outputs": {"synthetic_records_must_include_provenance": "synthetic_from_public_structure"},
        },
        {
            "scenario_id": "GT-ANSWER-GROUNDED-QUERY",
            "expected_agent": "search_chat_agent",
            "expected_decision": "answer_with_citations",
            "primary_reason": "response_must_ground_osimertinib_resistance_or_trial_claims_in_article_trial_dataset_or_label_entities",
            "top_policy_refs": ["HLS-TRIAL-300", "HLS-LIC-200"],
            "source_entities": ["RDOC-PMC6889286", "TRIAL-NCT02296125", "DATASET-GSE323366", "LBL-TAGRISSO-OPENFDA"],
            "expected_outputs": {"minimum_citation_count": 2, "must_include_raw_source_trace": True},
        },
    ]

    for case in cases:
        case.update(
            {
                "document_type": "decision_ground_truth",
                "document_date": "2026-06-17",
                "source_system": "hls_knowledge_mining_ground_truth",
                "required_human_review": False,
                "risk_flags": [],
                "summary_explanation": case["primary_reason"].replace("_", " "),
                "raw_sources": [rel(RAW / "raw_manifest.json")],
            }
        )
        write_json(FOLDERS["decision_ground_truth"] / f"{case['scenario_id']}.json", case)

    return cases


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

Experimental dataset and sample entities derived from GEO and synthetic LIMS raw files.

## Entity types

- `experimental_dataset`
- `assay_sample`
- `synthetic_lims_sample`

## Required fields

- dataset entities: `dataset_id`, `title`, `taxon`, `sample_count`, `sample_accessions`, `raw_sources`
- sample entities: `sample_id`, `dataset_id` or `source_geo_series`, `model`, `condition`, `raw_sources`
- all entities: `document_type`, `source_system`, `provenance`, `privacy_posture`
""",
        "biomarkers_targets": """# 04 Biomarkers and Targets Schema

Compound, target, and biomarker entities derived from ChEMBL plus curated raw-layer evidence.

## Entity types

- `compound`
- `molecular_target`
- `biomarker`

## Required fields

- `entity_id`
- `document_type`
- `preferred_name` or `name`
- `linked_compound_ids` where applicable
- `raw_sources`
- `provenance`
""",
        "regulatory_submissions": """# 05 Regulatory Submissions Schema

Regulatory application, label, and source-document entities derived from openFDA, Drugs@FDA, and EMA raw files.

## Entity types

- `regulatory_application`
- `product_label`
- `regulatory_source_document`

## Required fields

- `document_id`
- `document_type`
- `source_system`
- `application_number` where applicable
- `compound_id` or product identifiers where applicable
- `raw_sources`
- `provenance`
""",
        "policy_rag": """# 06 Policy RAG Schema

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
        "evidence_links": """# 07 Evidence Links Schema

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
        "curation_decisions": """# 08 Curation Decisions Schema

Allow/deny/review decisions for source inclusion and compliance guardrails.

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

Expected agent outcomes for evaluating ingestion, linking, retrieval, and curation flows.

## Required fields

- `scenario_id`
- `document_type` = `decision_ground_truth`
- `expected_agent`
- `expected_decision`
- `primary_reason`
- `top_policy_refs`
- `source_entities`
- `expected_outputs`
- `required_human_review`
- `risk_flags`
- `summary_explanation`
- `raw_sources`
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
            {"folder": "04_biomarkers_and_targets", "entity_count": counts["biomarkers_targets"]},
            {"folder": "05_regulatory_submissions", "entity_count": counts["regulatory_submissions"]},
            {"folder": "06_policy_rag", "entity_count": counts["policy_rag"]},
            {"folder": "07_evidence_links", "entity_count": counts["evidence_links"]},
            {"folder": "08_curation_decisions", "entity_count": counts["curation_decisions"]},
            {"folder": "09_decision_ground_truth", "entity_count": counts["decision_ground_truth"]},
        ],
        "privacy_posture": {
            "patient_level_data": "excluded",
            "patient_names": "excluded",
            "synthetic_people": "fictional lab/reviewer names only",
            "selected_geo_records": "cell-line or assay-level records only",
        },
    }
    write_json(BASE / "dataset-manifest.json", manifest)


def main() -> int:
    catalog = load_json(CATALOG_PATH)
    if not RAW.exists():
        raise RuntimeError("Raw layer is missing. Run generate_raw_layer.py first.")

    reset_output()
    research_docs = generate_research_documents(catalog)
    trials = generate_clinical_trials(catalog)
    datasets, samples = generate_experimental_datasets(catalog)
    biomarkers = generate_biomarkers_targets(catalog)
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
        "biomarkers_targets": len(biomarkers),
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
