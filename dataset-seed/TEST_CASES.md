# Test Cases — Two HLS flows (ingestion + search)

HLS is **two separate processes, decoupled in time** (no single orchestrator):

- **Ingestion** (`ING-*`) — *load* knowledge: `upload → ingestion/translation → metadata linking →
  human approval → persistence into the CMS/knowledge base`.
- **Search** (`QRY-*`) — *query* that knowledge: `query → search/chat → curation/compliance review
  → response`. Runs later, against what was already persisted.

Each flow is started by a **controlled UI action** (button/process trigger, not a chatbot). The
scenario set is defined once in [`scenarios.py`](scenarios.py).

For the handoff contract (what each step receives/produces) see [HANDOFF.md](HANDOFF.md). For the
plain-language **demo runbook** (inject the prepared folders, no terminal needed) see
[TESTING_GUIDE.md](TESTING_GUIDE.md).

## Scenario index

| Scenario | Flow | Path | Final outcome | What it stresses |
| --- | --- | --- | --- | --- |
| `ING-001` | ingestion | `full_approval` | `approved_persisted` | Clean upload: 5 OA articles license-checked, FLAURA↔NDA208065 linked, human approves, persisted to KB. |
| `ING-002` | ingestion | `guardrail_review` | `needs_human_review` | Ingestion denies a no-license article (`PMC4771182`); linking excludes patient-derived GEO (`GSE297057`, `GSE301973`); approval gate `denied_pending_human_review`, nothing persisted. |
| `ING-003` | ingestion | `synthetic_provenance` | `approved_with_required_labeling` | Synthetic ELN/LIMS load; human approves with `synthetic_from_public_structure` labeling. |
| `ING-004` | ingestion | `sensitive_blocked` | `denied_not_persisted` | Patient-derived candidate (`GSE301973`) blocked at the human-approval gate; never persisted. |
| `QRY-001` | search | `no_data` | `no_grounded_answer` | Query an **empty** KB → no grounded answer (demo step 1). |
| `QRY-002` | search | `grounded` | `answer_with_citations` | Query a **populated** KB (after `ING-001`) → grounded answer with ≥2 citations (demo step 3). |

**Headline demo (stateful):** `QRY-001` (search, no data) → `ING-001` (ingest) → `QRY-002` (search
again, the answer appears).

## Where the cases live

Two aligned views of the same scenario set:

```text
09_decision_ground_truth/<ID>.json     <- e2e rollup (flow + trigger + ordered stages + gates + final outcome)
00_raw/<ID>_<path>/                     <- self-contained per-stage folders to RUN the flow

  # ingestion                            # search
  01_upload/         trigger.json          01_query/          trigger.json
  02_ingestion_translation/  (agent)       02_search_chat/          (agent)
  03_metadata_linking/       (agent)       03_curation_compliance/  (agent)
  04_human_approval/  gate.json            04_response/       response.json
  05_persistence/     persisted.json
  scenario.json   <- mirror of the 09 rollup
```

An *agent* stage has `agent_input.json` + `input/` + `expected_output/`. Other stages have a single
primary file (`trigger.json` / `gate.json` / `persisted.json` / `response.json`).

## Expected results per scenario

The full answer key is each scenario's `scenario.json` (mirrored from the `09` rollup). Decisive
per-stage outcomes:

**ING-001 `full_approval` → `approved_persisted`**

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 1 | upload | trigger | — | — | 5 uploaded articles |
| 2 | ingestion_translation | agent | `approve` | — | 5× `RDOC-*` |
| 3 | metadata_linking | agent | `approve` | — | 2× `LINK-*`, `CMP-CHEMBL3353410`, `TGT-CHEMBL203` |
| 4 | human_approval | gate | `approve` | `approved` | — |
| 5 | persistence | sink | — | — | 9 entities persisted to KB |

**ING-002 `guardrail_review` → `needs_human_review`**

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 2 | ingestion_translation | agent | `approve_with_exclusions` | — | 5× `RDOC-*` **+ `CUR-EXCLUDE-PMC4771182`** |
| 3 | metadata_linking | agent | `approve_with_exclusions` | — | 5× `DATASET-GSE*` **+ `CUR-EXCLUDE-GSE297057/GSE301973`** |
| 4 | human_approval | gate | `flag_for_human_review` | **`denied_pending_human_review`** | — |
| 5 | persistence | sink | — | — | **none** (pending) |

**ING-003 `synthetic_provenance` → `approved_with_required_labeling`**

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 2 | ingestion_translation | agent | `approve` | — | `SYN-LIMS-001`, `SYN-LIMS-010` |
| 3 | metadata_linking | agent | `approve` | — | `DATASET-GSE323366` |
| 4 | human_approval | gate | `approve_with_required_labeling` | **`approved_with_labeling`** | — |
| 5 | persistence | sink | — | — | persisted with `synthetic_from_public_structure` label |

**ING-004 `sensitive_blocked` → `denied_not_persisted`**

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 2 | ingestion_translation | agent | `defer_to_human_approval` | — | — (metadata only) |
| 3 | metadata_linking | agent | `no_action` | — | — (nothing admitted) |
| 4 | human_approval | gate | `deny` | **`denied`** | — |
| 5 | persistence | sink | — | — | **none** |

**QRY-001 `no_data` → `no_grounded_answer`** (KB `empty`)

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 2 | search_chat | agent | `no_grounded_answer` | — | — (empty KB) |
| 3 | curation_compliance | agent | `confirm_no_data_response` | `approved` | — |
| 4 | response | output | — | — | "no grounded information yet" |

**QRY-002 `grounded` → `answer_with_citations`** (KB `populated`)

| Order | Stage | Kind | Decision | Gate | Output |
| --- | --- | --- | --- | --- | --- |
| 2 | search_chat | agent | `answer_with_citations` | — | ≥2 citations + raw trace |
| 3 | curation_compliance | agent | `approve_response` | `approved` | — |
| 4 | response | output | — | — | grounded answer + citations |

## How to trace a stage to raw files

```text
09_decision_ground_truth/<ID>.json
  -> stages[].output_entities[]
  -> matching normalized entity in 01_* .. 08_*
  -> that entity's raw_sources[]
  -> concrete files under 00_raw/_corpus/  (also duplicated into the stage's input/ or expected_output/)
```

The canonical corpus in `00_raw/_corpus/` stays the single source of truth referenced by
`raw_manifest.json` and every normalized entity's `raw_sources`. The `ING-*/` and `QRY-*/` folders
are deterministic duplicates rebuilt by [`build_scenario_folders.py`](build_scenario_folders.py).

## Dataset self-check (maintainers)

Validates the scenario plumbing — every produced entity resolves to a normalized entity file. Run
from `dataset-seed/`:

```bash
for f in 09_decision_ground_truth/ING-*.json 09_decision_ground_truth/QRY-*.json; do
  echo "## $f"
  jq -r '.stages[].output_entities[]?' "$f" | sort -u | while read e; do
    [ -z "$e" ] && continue
    hit=$(find 01_* 02_* 03_* 04_* 05_* 06_* 07_* 08_* -name "$e.json" | head -1)
    [ -n "$hit" ] && echo "  ok   $e -> $hit" || echo "  MISS $e"
  done
done
```

Every line should read `ok` (no `MISS`).

## Regenerate

```bash
cd dataset-seed
python3 generate_raw_layer.py          # fetch/synthesize -> 00_raw/_corpus/  (needs network)
python3 generate_normalized_layers.py  # normalized entities + 09 ING/QRY rollups (reads scenarios.py)
python3 generate_agent_documents.py    # multi-format replicas in _corpus/
python3 build_scenario_folders.py      # (offline) build 00_raw/{ING,QRY}-*/ per-stage folders
```
