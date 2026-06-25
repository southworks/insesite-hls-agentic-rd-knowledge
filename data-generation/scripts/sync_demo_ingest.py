#!/usr/bin/env python3
"""
Regenerate dataset-seed demo ingest/ folders from data-generation expected-outputs.

Maps Case folders and demo-flow steps to legacy scenario IDs, then copies only the
upload/raw files from the ingestion_translation stage (no JSON entity handoffs).
"""

from __future__ import annotations

import shutil
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent
DATA_GEN = SCRIPTS.parent
REPO = DATA_GEN.parent
EXPECTED = DATA_GEN / "expected-outputs"
DATASET_SEED = REPO / "dataset-seed"

# demo folder -> (legacy scenario folder under expected-outputs, stage subdir)
INGEST_SOURCES: dict[str, tuple[str, str]] = {
    "cases/case-02-human-review": ("ING-002_guardrail_review", "01_ingestion_translation"),
    "cases/case-03-approval-labeling": ("ING-003_synthetic_provenance", "01_ingestion_translation"),
    "cases/case-04-sensitive-denied": ("ING-004_sensitive_blocked", "01_ingestion_translation"),
    "demo-flow/step-02-full-approval": ("DEMO_SCENARIO/2-ING-001_full_approval", "01_ingestion_translation"),
}


def sync_ingest(demo_rel: str, scenario_name: str, stage: str) -> int:
    src = EXPECTED / scenario_name / stage / "input"
    dst = DATASET_SEED / demo_rel / "ingest"
    if not src.is_dir():
        raise FileNotFoundError(f"missing ingest source: {src}")
    if dst.exists():
        shutil.rmtree(dst)
    shutil.copytree(src, dst)
    return sum(1 for _ in dst.rglob("*") if _.is_file())


def main() -> None:
    total = 0
    for demo_rel, (scenario_name, stage) in INGEST_SOURCES.items():
        count = sync_ingest(demo_rel, scenario_name, stage)
        print(f"{demo_rel}: {count} files")
        total += count
    print(f"\nDone — {total} ingest files synced to dataset-seed/")


if __name__ == "__main__":
    main()
