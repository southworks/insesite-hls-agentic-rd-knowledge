#!/usr/bin/env python3
"""
Regenerate dataset-seed demo ingest/ folders from data-generation expected-outputs.

Maps Case folders to legacy scenario IDs, then copies only the upload/raw files
from the ingestion_translation stage (no JSON entity handoffs).
"""

from __future__ import annotations

import shutil
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent
DATA_GEN = SCRIPTS.parent
REPO = DATA_GEN.parent
EXPECTED = DATA_GEN / "expected-outputs"
DATASET_SEED = REPO / "dataset-seed"

# dataset-seed folder -> (legacy scenario folder under expected-outputs, stage subdir)
INGEST_SOURCES: dict[str, tuple[str, str]] = {
    "cases/case-01-human-review": ("ING-002_guardrail_review", "01_ingestion_translation"),
    "cases/case-02-approval-labeling": ("ING-003_synthetic_provenance", "01_ingestion_translation"),
    "cases/case-03-sensitive-denied": ("ING-004_sensitive_blocked", "01_ingestion_translation"),
    "cases/case-04-demo/step-02-full-approval": (
        "DEMO_SCENARIO/2-ING-001_full_approval",
        "01_ingestion_translation",
    ),
}


def copy_ingest_file(src_file: Path, src_root: Path, dst_root: Path) -> bool:
    rel = src_file.relative_to(src_root)
    if rel.name in {"europe_pmc_metadata.json", "pmc_oa_license.xml"}:
        return False

    if rel.parts[-1] == "article.xml" and "pmc_oa" in rel.parts:
        pmcid = rel.parts[-2]
        dst_file = dst_root / f"{pmcid}_article.xml"
    else:
        dst_file = dst_root / rel.name

    if dst_file.exists():
        raise FileExistsError(f"flattened ingest filename collision: {dst_file}")

    dst_file.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src_file, dst_file)
    return True


def sync_ingest(demo_rel: str, scenario_name: str, stage: str) -> int:
    src = EXPECTED / scenario_name / stage / "input"
    dst = DATASET_SEED / demo_rel / "ingest"
    if not src.is_dir():
        raise FileNotFoundError(f"missing ingest source: {src}")
    if dst.exists():
        shutil.rmtree(dst)
    dst.mkdir(parents=True, exist_ok=True)
    return sum(copy_ingest_file(src_file, src, dst) for src_file in src.rglob("*") if src_file.is_file())


def main() -> None:
    total = 0
    for demo_rel, (scenario_name, stage) in INGEST_SOURCES.items():
        count = sync_ingest(demo_rel, scenario_name, stage)
        print(f"{demo_rel}: {count} files")
        total += count
    print(f"\nDone — {total} ingest files synced to dataset-seed/")


if __name__ == "__main__":
    main()
