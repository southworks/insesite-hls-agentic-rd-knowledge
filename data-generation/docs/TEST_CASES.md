# Test Cases — Two HLS phases (ingestion + search)

HLS is **two sequential phases**, each closed by a distinct human actor:

- **Phase 1 — Ingestion** (`ING-*`): `ingestion & translation → metadata & linking →
  [knowledge curator approves] → persistence into the CMS/knowledge base`.
- **Phase 2 — Search** (`QRY-*`): `search & chat → curation & compliance →
  [compliance owner approves] → response`. Runs after phase 1 — immediately or deferred.

Each phase is started by a **controlled UI action** (button/process trigger, not a chatbot). The
scenario set is defined once in [`scenarios.py`](scenarios.py).

> The demo traverses **every** agent + both human actors, but datasets are materialized **only for
> the data-consuming agents** (+ the response output). The human approvals and persistence are
> **memory stages** — present in each `scenario.json` answer key, with no raw-layer folder.

For the handoff contract see [HANDOFF.md](HANDOFF.md); for the plain-language demo runbook see
[TESTING_GUIDE.md](TESTING_GUIDE.md).

## Scenario index

| Scenario | Phase | Path | Final outcome | What it stresses | Location |
| --- | --- | --- | --- | --- | --- |
| `ING-001` | 1 | `full_approval` | `approved_persisted` | Clean upload: 5 OA articles license-checked, FLAURA↔NDA208065 linked, curator approves, persisted. | `DEMO_SCENARIO/2-…` |
| `ING-002` | 1 | `guardrail_review` | `needs_human_review` | Ingestion denies a no-license article (`PMC4771182`); linking excludes patient-derived GEO (`GSE297057`, `GSE301973`); curator gate `denied_pending_human_review`, nothing persisted. | `00_raw/ING-002_…` |
| `ING-003` | 1 | `synthetic_provenance` | `approved_with_required_labeling` | Synthetic ELN/LIMS load; curator approves with `synthetic_from_public_structure` labeling. | `00_raw/ING-003_…` |
| `ING-004` | 1 | `sensitive_blocked` | `denied_not_persisted` | Patient-derived candidate (`GSE301973`) blocked at the curator gate; never persisted. | `00_raw/ING-004_…` |
| `QRY-001` | 2 | `no_data` | `no_grounded_answer` | Query an **empty** KB → no grounded answer (demo step 1). | `DEMO_SCENARIO/1-…` |
| `QRY-002` | 2 | `grounded` | `answer_with_citations` | Query a **populated** KB (after `ING-001`) → grounded answer with ≥2 citations (demo step 3). | `DEMO_SCENARIO/3-…` |

**Headline demo (stateful):** `1-QRY-001` (no data) → `2-ING-001` (ingest) → `3-QRY-002` (answer
appears), bundled under `00_raw/DEMO_SCENARIO/`.

## Where the cases live

Two aligned views of the same scenario set:

```text
09_decision_ground_truth/<ID>.json     <- full e2e answer key (trigger + EVERY stage + gates + outcome)
00_raw/.../<ID>_<path>/                 <- self-contained folders to RUN the data-consuming stages

  # phase 1 (ingestion)                  # phase 2 (search)
  01_ingestion_translation/  (agent)      01_search_chat/          (agent)
  02_metadata_linking/       (agent)      02_curation_compliance/  (agent)
  scenario.json                           03_response/      response.json
                                          scenario.json
```

A materialized **agent** stage has `agent_input.json` + `input/` + `expected_output/`. The
**response** stage has `response.json` only. The **curator/compliance approvals and persistence**
have no folder — they are the `materialized: false` entries in `scenario.json` → `stages[]`.

## Expected results per scenario

The full answer key is each scenario's `scenario.json`. Decisive per-stage outcomes (M = materialized
folder, mem = memory stage in `scenario.json` only):

**ING-001 `full_approval` → `approved_persisted`**

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 1 | ingestion_translation | agent (M) | `approve` | — | 5× `RDOC-*` |
| 2 | metadata_linking | agent (M) | `approve` | — | 2× `LINK-*`, `CMP-CHEMBL3353410`, `TGT-CHEMBL203` |
| 3 | curator_approval | gate (mem) | `approve` | `approved` | — |
| 4 | persistence | sink (mem) | — | — | 9 entities persisted to KB |

**ING-002 `guardrail_review` → `needs_human_review`**

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 1 | ingestion_translation | agent (M) | `approve_with_exclusions` | — | 5× `RDOC-*` **+ `CUR-EXCLUDE-PMC4771182`** |
| 2 | metadata_linking | agent (M) | `approve_with_exclusions` | — | 5× `DATASET-GSE*` **+ `CUR-EXCLUDE-GSE297057/GSE301973`** |
| 3 | curator_approval | gate (mem) | `flag_for_human_review` | **`denied_pending_human_review`** | — |
| 4 | persistence | sink (mem) | — | — | **none** (pending) |

**ING-003 `synthetic_provenance` → `approved_with_required_labeling`**

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 1 | ingestion_translation | agent (M) | `approve` | — | `SYN-LIMS-001`, `SYN-LIMS-010` |
| 2 | metadata_linking | agent (M) | `approve` | — | `DATASET-GSE323366` |
| 3 | curator_approval | gate (mem) | `approve_with_required_labeling` | **`approved_with_labeling`** | — |
| 4 | persistence | sink (mem) | — | — | persisted with `synthetic_from_public_structure` label |

**ING-004 `sensitive_blocked` → `denied_not_persisted`**

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 1 | ingestion_translation | agent (M) | `defer_to_human_approval` | — | — (metadata only) |
| 2 | metadata_linking | agent (M) | `no_action` | — | — (nothing admitted) |
| 3 | curator_approval | gate (mem) | `deny` | **`denied`** | — |
| 4 | persistence | sink (mem) | — | — | **none** |

**QRY-001 `no_data` → `no_grounded_answer`** (KB `empty`)

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 1 | search_chat | agent (M) | `no_grounded_answer` | — | — (empty KB) |
| 2 | curation_compliance | agent (M) | `confirm_no_data_response` | — | — |
| 3 | compliance_approval | gate (mem) | `approve` | `approved` | — |
| 4 | response | output (M) | — | — | "no grounded information yet" |

**QRY-002 `grounded` → `answer_with_citations`** (KB `populated`)

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 1 | search_chat | agent (M) | `answer_with_citations` | — | ≥2 citations + raw trace |
| 2 | curation_compliance | agent (M) | `approve_response` | — | — |
| 3 | compliance_approval | gate (mem) | `approve` | `approved` | — |
| 4 | response | output (M) | — | — | grounded answer + citations |

## How to trace a stage to raw files

```text
09_decision_ground_truth/<ID>.json
  -> stages[].output_entities[]
  -> matching normalized entity in the root catalog (01_* .. 07_*)
  -> that entity's raw_sources[]
  -> concrete files under 00_raw/_corpus/  (also duplicated into the stage's input/ or expected_output/)
```

The canonical corpus in `00_raw/_corpus/` stays the single source of truth referenced by
`raw_manifest.json` and every normalized entity's `raw_sources`. The scenario folders are
deterministic duplicates rebuilt by [`build_scenario_folders.py`](build_scenario_folders.py).

## Dataset self-check (maintainers)

Validates the scenario plumbing — every produced entity resolves to a root-catalog entity file. Run
from `dataset-seed/`:

```bash
for f in 09_decision_ground_truth/ING-*.json 09_decision_ground_truth/QRY-*.json; do
  echo "## $f"
  jq -r '.stages[].output_entities[]?' "$f" | sort -u | while read e; do
    [ -z "$e" ] && continue
    hit=$(find 01_* 02_* 03_* 04_* 05_* 06_* 07_* -name "$e.json" | head -1)
    [ -n "$hit" ] && echo "  ok   $e -> $hit" || echo "  MISS $e"
  done
done
```

Every line should read `ok` (no `MISS`).

## Regenerate

```bash
cd dataset-seed
python3 generate_raw_layer.py          # fetch/synthesize -> 00_raw/_corpus/  (needs network)
python3 generate_normalized_layers.py  # trimmed root catalog + 09 ING/QRY rollups (reads scenarios.py)
python3 generate_agent_documents.py    # multi-format replicas in _corpus/
python3 build_scenario_folders.py      # (offline) build DEMO_SCENARIO/ + standalone ING-* folders
```
