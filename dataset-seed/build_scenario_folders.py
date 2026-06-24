#!/usr/bin/env python3
"""
Build the per-scenario, per-agent Raw Layer folders for the HLS dataset seed.

HLS is **two isolated flows** (no single orchestrator), decoupled in time:

    INGESTION: upload -> ingestion/translation -> metadata linking -> human approval -> persistence
    SEARCH:    query  -> search/chat -> curation/compliance review -> response

The scenario set is defined once in `scenarios.py` (imported here and by
generate_normalized_layers.py). The canonical corpus in 00_raw/_corpus/ (produced by
generate_raw_layer.py) is the single source of truth. This script creates, per scenario, one
folder with a sub-folder per stage:

    00_raw/ING-XXX_<path>/                  00_raw/QRY-XXX_<path>/
      01_upload/         trigger.json         01_query/          trigger.json
      02_ingestion_translation/              02_search_chat/
        agent_input.json                       agent_input.json
        input/  expected_output/               input/  expected_output/
      03_metadata_linking/  (agent)          03_curation_compliance/ (agent)
      04_human_approval/    gate.json        04_response/       response.json
      05_persistence/       persisted.json
      scenario.json   <- mirror of 09_decision_ground_truth/<ID>.json

Each stage's primary file is named by its kind (trigger.json | agent_input.json | gate.json |
persisted.json | response.json). Only `agent` stages carry input/ + expected_output/.

Because HLS is entity-based, raw files and normalized entities are intentionally duplicated
into every stage/scenario that uses them; the canonical copies in _corpus/ and the normalized
01_*..08_* layers remain the source referenced by raw_manifest.json.

Idempotent: existing 00_raw/{ING,QRY}-*/ folders are removed and rebuilt. Offline (no fetch).
Run AFTER generate_normalized_layers.py (it copies normalized entity JSON into the folders).
"""

from __future__ import annotations

import json
import shutil
from pathlib import Path

from scenarios import SCENARIOS, scenario_folder, stage_folder, stage_primary

BASE = Path(__file__).resolve().parent
RAW = BASE / "00_raw"
CORPUS_PREFIX = "00_raw/_corpus/"
GT_DIR = BASE / "09_decision_ground_truth"

ENTITY_LAYERS = [
    "01_research_documents", "02_clinical_trials", "03_experimental_datasets",
    "04_biomarkers_and_targets", "05_regulatory_submissions", "06_policy_rag",
    "07_evidence_links", "08_curation_decisions",
]


def entity_index() -> dict[str, Path]:
    index: dict[str, Path] = {}
    for layer in ENTITY_LAYERS:
        for jf in (BASE / layer).glob("*.json"):
            index[jf.stem] = jf
    return index


def raw_sources_for(entity_file: Path) -> list[str]:
    data = json.loads(entity_file.read_text(encoding="utf-8"))
    return [s for s in data.get("raw_sources", []) if s.startswith(CORPUS_PREFIX)]


def copy_raw(rel: str, dest_dir: Path, warnings: list[str], scenario_id: str) -> int:
    src = BASE / rel
    if not src.is_file():
        warnings.append(f"  WARNING [{scenario_id}] missing raw source: {rel}")
        return 0
    sub = rel[len(CORPUS_PREFIX):] if rel.startswith(CORPUS_PREFIX) else Path(rel).name
    dest = dest_dir / sub
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dest)
    return 1


def copy_entity(entity_id: str, dest_dir: Path, index: dict[str, Path], warnings: list[str], scenario_id: str) -> int:
    entity_file = index.get(entity_id)
    if entity_file is None:
        warnings.append(f"  WARNING [{scenario_id}] unresolved entity: {entity_id}")
        return 0
    dest_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(entity_file, dest_dir / entity_file.name)
    return 1


def build_stage(scenario: dict, stage: dict, scenario_dir: Path, index: dict[str, Path],
                warnings: list[str]) -> int:
    scenario_id = scenario["scenario_id"]
    stage_dir = scenario_dir / stage_folder(stage)
    stage_dir.mkdir(parents=True, exist_ok=True)
    files = 0

    # Primary file, named by stage kind (trigger.json | agent_input.json | gate.json | ...).
    filename, _key, content = stage_primary(stage)
    (stage_dir / filename).write_text(
        json.dumps(content, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    # input/ — documents/entities the stage starts from.
    input_raw = list(stage.get("input_raw", []))
    for eid in stage.get("input_entities_raw", []):          # raw behind uploaded entities
        ef = index.get(eid)
        if ef is not None:
            input_raw += raw_sources_for(ef)
    if stage.get("input_from_output_raw"):                   # raw the agent ingests behind its output
        for eid in stage.get("output_entities", []):
            ef = index.get(eid)
            if ef is not None:
                input_raw += raw_sources_for(ef)
    input_entities = stage.get("input_entities", [])
    # Agent stages always get an input/ (possibly empty, e.g. a query against an empty KB).
    if input_raw or input_entities or stage["kind"] == "agent":
        input_dir = stage_dir / "input"
        input_dir.mkdir(parents=True, exist_ok=True)
        for rel in sorted(set(input_raw)):
            files += copy_raw(rel, input_dir, warnings, scenario_id)
        for eid in input_entities:
            files += copy_entity(eid, input_dir, index, warnings, scenario_id)

    # expected_output/ — only agent stages produce a measurable expectation + output entities.
    if stage["kind"] == "agent":
        output_dir = stage_dir / "expected_output"
        output_dir.mkdir(parents=True, exist_ok=True)
        for eid in stage.get("output_entities", []):
            files += copy_entity(eid, output_dir, index, warnings, scenario_id)
        (output_dir / "_expected_output.json").write_text(
            json.dumps({
                "stage": stage["stage"], "agent": stage.get("agent"), "decision": stage.get("decision"),
                "gate": stage.get("gate"), "output_entities": stage.get("output_entities", []),
                "expected_output": stage.get("expected_output"),
            }, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return files


def main() -> None:
    index = entity_index()

    # Clear existing scenario folders (everything in 00_raw/ except _corpus).
    for child in RAW.iterdir():
        if child.is_dir() and child.name != "_corpus":
            shutil.rmtree(child)

    total_files = 0
    warnings: list[str] = []
    for scenario in SCENARIOS:
        scenario_dir = RAW / scenario_folder(scenario)
        files = 0
        for stage in scenario["stages"]:
            files += build_stage(scenario, stage, scenario_dir, index, warnings)

        # scenario.json — mirror of the 09 rollup, for a self-contained demo folder.
        gt_file = GT_DIR / f"{scenario['scenario_id']}.json"
        if gt_file.is_file():
            shutil.copy2(gt_file, scenario_dir / "scenario.json")

        total_files += files
        print(f"{scenario_folder(scenario)}: {files} files across {len(scenario['stages'])} stages")

    for w in warnings:
        print(w)
    print(f"\nDone — {total_files} files duplicated into per-scenario / per-agent folders "
          f"under {RAW.relative_to(BASE.parent)}/ ({len(SCENARIOS)} scenarios)")


if __name__ == "__main__":
    main()
