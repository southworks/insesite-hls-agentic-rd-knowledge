#!/usr/bin/env python3
"""
Build the per-scenario Raw Layer folders for the HLS knowledge-mining dataset seed.

Only the agents that **consume data** are materialized (ingestion & translation, metadata & linking,
search & chat, curation & compliance) plus the final response. The human-approval gates and
persistence are memory stages — they live in `scenario.json` only (see scenarios.py / HANDOFF.md).

The normalized entity catalog under `data-generation/entity-catalog/` and the rollups in
`data-generation/ground-truth/` are the single sources of truth; this script copies the relevant
entities into each stage's `input/` and `expected_output/`.

    expected-outputs/DEMO_SCENARIO/<n>-<ID>_<path>/   (headline stateful demo: QRY-001 -> ING-001 -> QRY-002)
    expected-outputs/<ID>_<path>/                     (standalone guardrail variants: ING-002/003/004)
      <NN>_<stage>/
        agent_input.json        <- the handoff contract that starts that agent in isolation
        input/                  <- upstream handoff: uploaded raw (ingestion) / entities / prompt.txt
        expected_output/        <- entities this stage produces + _expected_output.json (validation)
      <NN>_response/
        response.json           <- the final returned answer (search scenarios)
      scenario.json             <- full e2e answer key (mirror of 09_decision_ground_truth/<ID>.json)

Idempotent: existing expected-outputs/ scenario children are removed and rebuilt. Offline (no fetch).
Run AFTER generate_normalized_layers.py.
"""

from __future__ import annotations

import json
import shutil
from pathlib import Path

from scenarios import (
    SCENARIOS, DEMO_SEQUENCE, scenario_folder, materialized_stages, stage_folder,
    demo_prefix,
)

SCRIPTS = Path(__file__).resolve().parent
DATA_GEN = SCRIPTS.parent
OUTPUT = DATA_GEN / "expected-outputs"
CORPUS = DATA_GEN / "corpus"
GT_DIR = DATA_GEN / "ground-truth" / "09_decision_ground_truth"
CATALOG = DATA_GEN / "entity-catalog"

# Root normalized entity catalog — every entity id resolves to exactly one of these folders.
CATALOG_FOLDERS = [
    CATALOG / "01_research_documents",
    CATALOG / "02_clinical_trials",
    CATALOG / "03_experimental_datasets",
    CATALOG / "04_regulatory_submissions",
    CATALOG / "05_compounds_targets",
    CATALOG / "06_evidence_links",
    CATALOG / "07_curation_decisions",
]


def build_entity_index() -> dict[str, Path]:
    """entity_id -> root catalog json path (RDOC-..., TRIAL-..., DATASET-..., CUR-EXCLUDE-..., ...)."""
    index: dict[str, Path] = {}
    for folder in CATALOG_FOLDERS:
        for fp in sorted(folder.glob("*.json")):
            index[fp.stem] = fp
    return index


ENTITY_INDEX = build_entity_index()


def _write_json(path: Path, data: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _copy_corpus_ref(ref: str, input_dir: Path, warnings: list) -> int:
    """Copy a `00_raw/_corpus/<...>` file into input/, preserving the path after `_corpus/`."""
    rel = ref.split("_corpus/", 1)[-1] if "_corpus/" in ref else ref
    src = CORPUS / rel
    if not src.is_file():
        warnings.append(f"missing corpus file: {ref}")
        return 0
    dst = input_dir / rel
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    return 1


def _copy_entity(entity_id: str, dest_dir: Path, warnings: list) -> int:
    src = ENTITY_INDEX.get(entity_id)
    if src is None:
        warnings.append(f"unresolved entity: {entity_id}")
        return 0
    dest_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dest_dir / src.name)
    return 1


def build_agent_stage(scenario: dict, stage: dict, stage_dir: Path, warnings: list) -> int:
    """Materialize a data-consuming agent stage: agent_input.json + input/ + expected_output/."""
    files = 0
    _write_json(stage_dir / "agent_input.json", stage["agent_input"])
    files += 1

    # --- input/ : the upstream handoff this agent starts from ---
    input_dir = stage_dir / "input"
    input_dir.mkdir(parents=True, exist_ok=True)

    # uploaded raw articles, behind the accepted entities (ingestion via upload)
    for entity_id in stage.get("input_entities_raw", []):
        src = ENTITY_INDEX.get(entity_id)
        if src is None:
            warnings.append(f"unresolved entity (raw): {entity_id}")
            continue
        for ref in json.loads(src.read_text(encoding="utf-8")).get("raw_sources", []):
            if "_corpus/" in ref:
                files += _copy_corpus_ref(ref, input_dir, warnings)

    # explicit corpus files (synthetic ELN/LIMS, curation-decision txt)
    for ref in stage.get("input_raw", []):
        files += _copy_corpus_ref(ref, input_dir, warnings)

    # entity handoff from upstream (RDOCs, SYN-LIMS, retrieved KB entities, citations)
    for entity_id in stage.get("input_entities", []):
        files += _copy_entity(entity_id, input_dir, warnings)

    # the controlled query, as a prompt file (search & chat)
    if stage.get("prompt"):
        (input_dir / "prompt.txt").write_text(stage["prompt"].strip() + "\n", encoding="utf-8")
        files += 1

    # --- expected_output/ : what this agent would produce ---
    output_dir = stage_dir / "expected_output"
    output_dir.mkdir(parents=True, exist_ok=True)
    for entity_id in stage.get("output_entities", []):
        files += _copy_entity(entity_id, output_dir, warnings)
    _write_json(output_dir / "_expected_output.json", {
        "stage": stage["stage"],
        "agent": stage.get("agent"),
        "decision": stage.get("decision"),
        "gate": stage.get("gate"),
        "output_entities": stage.get("output_entities", []),
        "expected_output": stage.get("expected_output"),
    })
    files += 1
    return files


def build_output_stage(stage: dict, stage_dir: Path) -> int:
    """Materialize the response output stage: response.json only."""
    _write_json(stage_dir / "response.json", stage["response"])
    return 1


def scenario_base_dir(scenario: dict) -> Path:
    sid = scenario["scenario_id"]
    if sid in DEMO_SEQUENCE:
        return OUTPUT / "DEMO_SCENARIO" / f"{demo_prefix(sid)}-{scenario_folder(scenario)}"
    return OUTPUT / scenario_folder(scenario)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for child in OUTPUT.iterdir():
        if child.is_dir():
            shutil.rmtree(child)

    all_warnings: list[str] = []
    total_files = 0
    for scenario in SCENARIOS:
        sid = scenario["scenario_id"]
        scenario_dir = scenario_base_dir(scenario)
        warnings: list[str] = []
        files = 0

        for stage in materialized_stages(scenario):
            stage_dir = scenario_dir / stage_folder(scenario, stage)
            stage_dir.mkdir(parents=True, exist_ok=True)
            if stage["kind"] == "agent":
                files += build_agent_stage(scenario, stage, stage_dir, warnings)
            elif stage["kind"] == "output":
                files += build_output_stage(stage, stage_dir)

        shutil.copy2(GT_DIR / f"{sid}.json", scenario_dir / "scenario.json")
        files += 1

        rel_dir = scenario_dir.relative_to(OUTPUT)
        flag = f"  WARN {len(warnings)}" if warnings else ""
        print(f"expected-outputs/{rel_dir}: {files} files across {len(materialized_stages(scenario))} stages{flag}")
        all_warnings += [f"{sid}: {w}" for w in warnings]
        total_files += files

    print(f"\nDone — {total_files} files written under expected-outputs/ ({len(SCENARIOS)} scenarios)")
    if all_warnings:
        print("\nWarnings:")
        for w in all_warnings:
            print(f"  - {w}")


if __name__ == "__main__":
    main()
