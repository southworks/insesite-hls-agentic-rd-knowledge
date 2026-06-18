# Raw Layer organized by scenario — process & resulting organization

Why and how the Raw layer was reorganized from *by format* to *by scenario / test case*, plus
narrative **demo flow stories** for starting a test run. Parallel to the loan and inventory
repos' write-ups.

## The problem

`00_raw/` used to be organized **by format** (`00_raw/{json,xml,pdf,html,txt,csv,md}/...`). To
exercise one evaluation case you had to chase its cited entities across format trees. The goal:
make each case a single, self-contained place an agent or demo can point at.

## Assessment — the raw↔scenario relationship differs per workflow

| Workflow | Model | Implication for a per-scenario layout |
|---|---|---|
| Loan | document-based — each applicant package belongs to one case | Clean partition, no duplication |
| Inventory | signal-based — one export carries signals for several scenarios | Canonical copy + sliced per-scenario duplicates |
| **R&D knowledge (this repo)** | **entity-based** — the same entity is cited by several cases (`RDOC-PMC6889286` in 2 cases, `TRIAL-NCT02296125` in 2) | Needs a **canonical corpus + duplicated per-scenario folders** |

R&D knowledge is *entity-based*: the cases are queries over a shared corpus. Forcing physical
scenario folders means **duplicating** shared entities into every case that cites them — done
here — while keeping one canonical corpus as the single source of truth.

## Decision

```
00_raw/
  _corpus/                 ← CANONICAL: all source files + raw_manifest.json (single source of truth)
  GT-INGEST-ARTICLES/      ← per-scenario DUPLICATES of the raw files each case cites
  GT-LINK-TRIAL-REGULATORY/
  GT-USE-CELL-LINE-DATASETS/
  GT-REQUIRE-SYNTHETIC-PROVENANCE/
  GT-ANSWER-GROUNDED-QUERY/
```

- `raw_manifest.json` and every normalized entity's `raw_sources` point into `_corpus/` (kept the
  single source of truth — **verified: 84 manifest files hash-match, 182 `raw_sources` resolve**).
- The `GT-*/` folders are rebuilt offline and deterministically by
  [`build_scenario_folders.py`](build_scenario_folders.py) from each case's `source_entities` →
  `raw_sources` (49 duplicated files across the 5 cases).
- Cross-cutting multi-format `agent_inputs/` replicas stay in `_corpus/` (not per-scenario).
- Because re-fetching the public sources requires network, the canonical files were relocated and
  re-hashed offline; the generators (`generate_raw_layer.py`, `generate_normalized_layers.py`,
  `generate_agent_documents.py`) now target `00_raw/_corpus/` so a future re-fetch stays correct.

See [RAW_LAYER.md](RAW_LAYER.md) and [TEST_CASES.md](TEST_CASES.md).

## Demo flow stories

### Story A — "Grounded Answer with Citations" (`GT-ANSWER-GROUNDED-QUERY`)

- **Start here:** `00_raw/GT-ANSWER-GROUNDED-QUERY/` — a self-contained mix of an article
  (`PMC6889286`), a trial (`NCT02296125`), a GEO dataset (`GSE323366`), and a regulatory label
  (Tagrisso), in their original formats.
- **Flow:** the Search/Chat agent answers an osimertinib question, retrieving across these
  entities and emitting citations with a Raw-Layer source trace back to `_corpus/`.
- **Expected:** `09_decision_ground_truth/GT-ANSWER-GROUNDED-QUERY.json` —
  `answer_with_citations`, ≥2 citations, raw-source trace required.
- **Value:** retrieval-augmented answering grounded in real public evidence with provenance.

### Story B — "Trial ↔ Regulatory Linking" (`GT-LINK-TRIAL-REGULATORY`)

- **Start here:** `00_raw/GT-LINK-TRIAL-REGULATORY/` — trials `NCT02296125` (FLAURA) and
  `NCT02151981` (AURA3) plus the openFDA `NDA208065` application and Tagrisso label.
- **Flow:** the Metadata-Linking agent connects trial and regulatory entities through shared
  osimertinib / Tagrisso / AZD9291 identifiers, producing the required evidence links.
- **Expected:** `09_decision_ground_truth/GT-LINK-TRIAL-REGULATORY.json` — `approve` with links
  `LINK-FLAURA-REG-NDA208065` and `LINK-REG-NDA208065-LBL-TAGRISSO-OPENFDA`.
- **Value:** cross-source entity resolution into an auditable evidence graph — the knowledge-mining
  analogue of the FSI cross-document reconciliation story.

## Reproduce

```bash
cd dataset-seed
python3 generate_raw_layer.py          # fetch/synthesize -> 00_raw/_corpus/  (needs network)
python3 generate_normalized_layers.py  # normalized entities from _corpus/
python3 generate_agent_documents.py    # multi-format replicas in _corpus/
python3 build_scenario_folders.py      # (offline) rebuild 00_raw/GT-*/ from _corpus/
```
