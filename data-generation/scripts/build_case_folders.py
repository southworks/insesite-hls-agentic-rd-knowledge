#!/usr/bin/env python3
"""
Build rd-knowledge-mining/backend/dataset-seed/cases/ demo folders from corpus exports.

Each case folder matches the committed demo layout:

    rd-knowledge-mining/backend/dataset-seed/cases/case-XX_<path>/
      README.md                     (preserved — not overwritten)
      ingest/                       flat Fabric upload payload

    rd-knowledge-mining/backend/dataset-seed/cases/case-04-demo/
      ingest/                       ING-001 upload files
      prompts/                      QRY-001 and QRY-002 query text

Run AFTER generate_raw_layer.py (and optionally generate_agent_documents.py for ING-004 txt).
"""

from __future__ import annotations

import shutil
from pathlib import Path

from generate_normalized_layers import (
    CATALOG_PATH,
    RAW,
    build_entity_index,
    load_json,
    normalize_raw_ref,
)
from scenarios import (
    CASE_FOLDERS,
    CASE_QUERY_PROMPTS,
    DEMO_CASE,
    DEMO_PROMPT_FILES,
    SCENARIOS_BY_ID,
)

SCRIPTS = Path(__file__).resolve().parent
DATA_GEN = SCRIPTS.parent
REPO = DATA_GEN.parent
RUNTIME_SEED = REPO / "rd-knowledge-mining" / "backend" / "dataset-seed"
CASES_DIR = RUNTIME_SEED / "cases"

SKIP_INGEST_NAMES = {"europe_pmc_metadata.json", "pmc_oa_license.xml"}


def corpus_path(ref: str) -> Path:
    normalized = normalize_raw_ref(ref)
    if normalized.startswith("corpus/"):
        normalized = normalized[len("corpus/") :]
    return RAW / normalized


def copy_ingest_file(src_file: Path, dst_root: Path) -> bool:
    rel = src_file.relative_to(RAW)
    if rel.name in SKIP_INGEST_NAMES:
        return False

    if rel.name == "article.xml" and "pmc_oa" in rel.parts:
        pmcid = rel.parts[-2]
        dst_file = dst_root / f"{pmcid}_article.xml"
    else:
        dst_file = dst_root / rel.name

    if dst_file.exists():
        raise FileExistsError(f"flattened ingest filename collision: {dst_file}")

    dst_file.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src_file, dst_file)
    return True


def copy_corpus_ref(ref: str, dst_root: Path, warnings: list[str]) -> int:
    src = corpus_path(ref)
    if not src.is_file():
        warnings.append(f"missing corpus file: {ref}")
        return 0
    return 1 if copy_ingest_file(src, dst_root) else 0


def ingestion_stage(scenario: dict) -> dict:
    for stage in scenario["stages"]:
        if stage["stage"] == "ingestion_translation":
            return stage
    raise KeyError(f"no ingestion_translation stage in {scenario['scenario_id']}")


def build_ingest(scenario: dict, entity_index: dict[str, dict], dst: Path) -> tuple[int, list[str]]:
    if dst.exists():
        shutil.rmtree(dst)
    dst.mkdir(parents=True)

    stage = ingestion_stage(scenario)
    warnings: list[str] = []
    count = 0

    for entity_id in stage.get("input_entities_raw", []):
        entity = entity_index.get(entity_id)
        if entity is None:
            warnings.append(f"unresolved entity: {entity_id}")
            continue
        for ref in entity.get("raw_sources", []):
            count += copy_corpus_ref(ref, dst, warnings)

    for ref in stage.get("input_raw", []):
        count += copy_corpus_ref(ref, dst, warnings)

    return count, warnings


def rebuild_ingest(case_name: str, scenario_id: str, entity_index: dict[str, dict]) -> tuple[int, list[str]]:
    scenario = SCENARIOS_BY_ID[scenario_id]
    ingest_dir = CASES_DIR / case_name / "ingest"
    return build_ingest(scenario, entity_index, ingest_dir)


def rebuild_demo_prompts() -> int:
    prompts_dir = CASES_DIR / DEMO_CASE / "prompts"
    if prompts_dir.exists():
        shutil.rmtree(prompts_dir)
    prompts_dir.mkdir(parents=True)
    count = 0
    for scenario_id, filename in DEMO_PROMPT_FILES.items():
        scenario = SCENARIOS_BY_ID[scenario_id]
        for stage in scenario["stages"]:
            if stage.get("prompt"):
                (prompts_dir / filename).write_text(stage["prompt"].strip() + "\n", encoding="utf-8")
                count += 1
                break
    return count


def rebuild_query_case_prompts(scenario_id: str, case_name: str) -> int:
    filenames = CASE_QUERY_PROMPTS[scenario_id]
    scenario = SCENARIOS_BY_ID[scenario_id]
    prompt_stages = [s for s in scenario["stages"] if s.get("prompt")]
    if len(filenames) != len(prompt_stages):
        raise ValueError(
            f"{scenario_id}: expected {len(filenames)} prompt files, found {len(prompt_stages)} prompt stages"
        )

    prompts_dir = CASES_DIR / case_name / "prompts"
    if prompts_dir.exists():
        shutil.rmtree(prompts_dir)
    prompts_dir.mkdir(parents=True)

    count = 0
    for filename, stage in zip(filenames, prompt_stages):
        (prompts_dir / filename).write_text(stage["prompt"].strip() + "\n", encoding="utf-8")
        count += 1
    return count


def main() -> None:
    if not RAW.is_dir():
        raise RuntimeError("Corpus is missing. Run generate_raw_layer.py first.")

    catalog = load_json(CATALOG_PATH)
    entity_index = build_entity_index(catalog)
    all_warnings: list[str] = []
    ingest_total = 0

    for scenario_id, case_name in CASE_FOLDERS.items():
        if SCENARIOS_BY_ID[scenario_id]["flow"] != "ingestion":
            continue
        count, warnings = rebuild_ingest(case_name, scenario_id, entity_index)
        rel = f"cases/{case_name}"
        flag = f"  WARN {len(warnings)}" if warnings else ""
        print(f"{rel}/ingest: {count} files{flag}")
        ingest_total += count
        all_warnings.extend(f"{scenario_id}: {w}" for w in warnings)

    count, warnings = rebuild_ingest(DEMO_CASE, "ING-001", entity_index)
    print(f"cases/{DEMO_CASE}/ingest: {count} files")
    ingest_total += count
    all_warnings.extend(f"ING-001: {w}" for w in warnings)

    prompt_count = rebuild_demo_prompts()
    print(f"cases/{DEMO_CASE}/prompts: {prompt_count} files")

    for scenario_id in CASE_QUERY_PROMPTS:
        case_name = CASE_FOLDERS[scenario_id]
        q_count = rebuild_query_case_prompts(scenario_id, case_name)
        print(f"cases/{case_name}/prompts: {q_count} files")
        prompt_count += q_count

    print(
        f"\nDone — {ingest_total} ingest files and {prompt_count} prompts "
        f"under {CASES_DIR.relative_to(REPO)}/"
    )
    if all_warnings:
        print("\nWarnings:")
        for w in all_warnings:
            print(f"  - {w}")


if __name__ == "__main__":
    main()
