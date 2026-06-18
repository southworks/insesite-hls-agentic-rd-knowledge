#!/usr/bin/env python3
"""
Build the per-scenario (test-case) Raw Layer folders for the HLS dataset seed.

The Raw layer is organized by scenario / test case. The single source of truth is
the canonical corpus in 00_raw/_corpus/ (produced by generate_raw_layer.py). This
script creates one folder per evaluation case under 00_raw/<SCENARIO-ID>/ that
*duplicates* the concrete raw files each case cites, so a demo or test can point at
one self-contained directory.

Because HLS is entity-based — the same entity (e.g. RDOC-PMC6889286, TRIAL-NCT02296125)
is cited by more than one case — those raw files are intentionally duplicated into
every case folder that uses them. The canonical copy in _corpus/ remains the single
source referenced by each normalized entity's `raw_sources` and by raw_manifest.json.

Trace per case:
    09_decision_ground_truth/<SCENARIO-ID>.json
      -> source_entities[]
      -> normalized entity in 01_*..08_*  (matched by "<id>.json")
      -> that entity's raw_sources[]  (00_raw/_corpus/...)
      -> copied to 00_raw/<SCENARIO-ID>/<path-after-_corpus/>

Idempotent: existing 00_raw/<GT-*>/ folders are removed and rebuilt. Offline (no fetch).
"""

from __future__ import annotations

import json
import shutil
from pathlib import Path

BASE = Path(__file__).resolve().parent
RAW = BASE / "00_raw"
CORPUS = RAW / "_corpus"
GT_DIR = BASE / "09_decision_ground_truth"
CORPUS_PREFIX = "00_raw/_corpus/"

# Layers that hold normalized entities referenced by GT source_entities.
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


def main() -> None:
    index = entity_index()

    # Clear existing scenario folders (everything in 00_raw/ except _corpus).
    for child in RAW.iterdir():
        if child.is_dir() and child.name != "_corpus":
            shutil.rmtree(child)

    total_files = 0
    for gt_file in sorted(GT_DIR.glob("GT-*.json")):
        gt = json.loads(gt_file.read_text(encoding="utf-8"))
        scenario_id = gt["scenario_id"]
        dest_root = RAW / scenario_id

        raw_paths: set[str] = set()
        missing_entities: list[str] = []
        for entity_id in gt.get("source_entities", []):
            entity_file = index.get(entity_id)
            if entity_file is None:
                missing_entities.append(entity_id)
                continue
            raw_paths.update(raw_sources_for(entity_file))

        copied = 0
        for rel in sorted(raw_paths):
            src = BASE / rel
            if not src.is_file():
                print(f"  WARNING [{scenario_id}] missing source: {rel}")
                continue
            dest = dest_root / rel[len(CORPUS_PREFIX):]
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dest)
            copied += 1

        total_files += copied
        note = f"  (unresolved entities: {', '.join(missing_entities)})" if missing_entities else ""
        print(f"{scenario_id}: {copied} files from {len(gt.get('source_entities', []))} entities{note}")

    print(f"\nDone — {total_files} files duplicated into per-scenario folders under {RAW.relative_to(BASE.parent)}/")


if __name__ == "__main__":
    main()
