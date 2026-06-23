# Test Cases — End-to-End Workflow Scenarios

The test cases are **end-to-end**: each scenario is one full path through the workflow
(Orchestrator → Ingestion & translation → Metadata & linking → Search & chat →
Curation & compliance). The four scenarios differ at the **human-in-the-loop gates**, so
together they exercise the distinct paths of the diagram. The scenario set is defined once
in [`scenarios.py`](scenarios.py).

For the per-agent handoff contract (what each stage receives and produces) see
[HANDOFF.md](HANDOFF.md).

## Scenario index

| Scenario | Path | Final outcome | What it stresses |
| --- | --- | --- | --- |
| `RKM-001` | `full_approval` | `approved` | Happy path: ingest 5 OA articles, link FLAURA↔NDA208065, grounded answer, curation approves. All gates Approved. |
| `RKM-002` | `guardrail_review` | `needs_human_review` | Ingestion denies a no-license article (`PMC4771182`); linking excludes patient-derived GEO (`GSE297057`, `GSE301973`); curation flags → gate `denied_pending_human_review`. |
| `RKM-003` | `synthetic_provenance` | `approved_with_required_labeling` | Synthetic ELN/LIMS flow; curation requires `synthetic_from_public_structure` labeling. |
| `RKM-004` | `curation_denied` | `denied` | Patient-derived candidate (`GSE301973`) blocked at the curation gate; search refuses (no grounded evidence). |

## Where the cases live

Two aligned views of the same scenario set:

```text
09_decision_ground_truth/RKM-XXX.json     <- e2e rollup (orchestrator request + ordered stages + gates + final outcome)
00_raw/RKM-XXX_<path>/                     <- self-contained per-agent folders to RUN the flow
  01_orchestrator/request.json
  02_ingestion_translation/   agent_input.json  input/  expected_output/
  03_metadata_linking/        agent_input.json  input/  expected_output/
  04_search_chat/             agent_input.json  input/  expected_output/
  05_curation_compliance/     agent_input.json  input/  expected_output/
  scenario.json               <- mirror of the 09 rollup
```

## Start the flow from any agent

Each stage folder is self-contained, so a demo can begin mid-chain "as if the previous
agents had already run":

- `agent_input.json` — the structured payload to **start** that agent in isolation.
- `input/` — the documents it starts from (raw files for ingestion; the upstream agent's
  normalized entities for the downstream agents).
- `expected_output/` — the entities + `_expected_output.json` that agent **would** produce
  (so you can feed a guaranteed output to the next stage without running the previous one).

Example — start at Search & chat in the happy path:

```bash
cat 00_raw/RKM-001_full_approval/04_search_chat/agent_input.json          # the NL query + scope
ls  00_raw/RKM-001_full_approval/04_search_chat/input/                    # the entities in retrieval scope
cat 00_raw/RKM-001_full_approval/04_search_chat/expected_output/_expected_output.json
```

## Step-by-step validation runbook

Follow this to the letter to validate each scenario. The stage folder for each `order` is:
`order 1 → 02_ingestion_translation`, `2 → 03_metadata_linking`, `3 → 04_search_chat`,
`4 → 05_curation_compliance`. Shorthand below: `SC=00_raw/RKM-XXX_<path>`.

### Step 0 — Preconditions (once)

```bash
cd dataset-seed
python3 build_scenario_folders.py        # (re)build the per-agent folders from scenarios.py
ls 00_raw/RKM-*/                          # 4 scenario folders must exist
```

### Step 1 — Generic procedure for ONE stage

Repeat for every stage, in `order`, of the scenario under test:

1. **Read what you feed the agent:**
   `cat $SC/<stage>/agent_input.json` (the structured task/query) and
   `ls $SC/<stage>/input/` (ingestion → raw `json/`,`xml/`; downstream → the upstream entities).
2. **Run the agent under test** with exactly that `agent_input.json` + `input/`.
3. **Assert the output** against `$SC/<stage>/expected_output/_expected_output.json`:
   - `decision` matches,
   - `gate` matches (`null` if no HITL gate at that stage),
   - `output_entities[]` matches the produced entity ids (and, for stages that persist
     entities, the per-entity JSON files in `expected_output/` are what should be written).
4. **Chain to the next stage without running this one:** the next stage's `input/` already
   contains this stage's guaranteed `expected_output/`, so you can start mid-chain at any point.

Show the expected for any stage:

```bash
SC=00_raw/RKM-001_full_approval
jq '{decision, gate, output_entities}' $SC/02_ingestion_translation/expected_output/_expected_output.json
```

### Step 2 — Per-scenario expected results (assert these in order)

**RKM-001 `full_approval` → `approved`**

| Order | Stage | Feed (`input/`) | Expected `decision` | `gate` | Expected `output_entities` |
| --- | --- | --- | --- | --- | --- |
| 1 | ingestion_translation | 5 OA articles (raw `xml/`,`json/`) | `approve` | `null` | 5× `RDOC-*` |
| 2 | metadata_linking | trials + regulatory entities | `approve` | `approved` | `LINK-FLAURA-REG-NDA208065`, `LINK-REG-NDA208065-LBL-TAGRISSO-OPENFDA`, `CMP-CHEMBL3353410`, `TGT-CHEMBL203` |
| 3 | search_chat | article+trial+dataset+label | `answer_with_citations` | `null` | — (≥2 citations, raw trace) |
| 4 | curation_compliance | the 5 `RDOC-*` | `approve` | `approved` | 5× `CUR-PMC*` |

**RKM-002 `guardrail_review` → `needs_human_review`**

| Order | Stage | Expected `decision` | `gate` | Expected `output_entities` |
| --- | --- | --- | --- | --- |
| 1 | ingestion_translation | `approve_with_exclusions` | `null` | 5× `RDOC-*` **+ `CUR-EXCLUDE-PMC4771182`** (no-license denied) |
| 2 | metadata_linking | `approve_with_exclusions` | `approved` | 5× accepted `DATASET-GSE*` **+ `CUR-EXCLUDE-GSE297057`, `CUR-EXCLUDE-GSE301973`** (patient-derived) |
| 3 | search_chat | `answer_with_citations` | `null` | — (answers over admitted corpus only) |
| 4 | curation_compliance | `flag_for_human_review` | **`denied_pending_human_review`** | the 3 `CUR-EXCLUDE-*` |

**RKM-003 `synthetic_provenance` → `approved_with_required_labeling`**

| Order | Stage | Expected `decision` | `gate` | Expected `output_entities` |
| --- | --- | --- | --- | --- |
| 1 | ingestion_translation | `approve` | `null` | `SYN-LIMS-001`, `SYN-LIMS-010` (provenance `synthetic_from_public_structure`) |
| 2 | metadata_linking | `approve` | `approved` | `DATASET-GSE323366` (synthetic samples mapped to public series) |
| 3 | search_chat | `answer_with_citations` | `null` | — (answer states synthetic provenance) |
| 4 | curation_compliance | `approve_with_required_labeling` | **`approved_with_labeling`** | `CUR-SYNTHETIC-ELN-LIMS` |

**RKM-004 `curation_denied` → `denied`**

| Order | Stage | Expected `decision` | `gate` | Expected `output_entities` |
| --- | --- | --- | --- | --- |
| 1 | ingestion_translation | `defer_to_curation` | `null` | — (metadata only, no payload stored) |
| 2 | metadata_linking | `no_action` | `null` | — (nothing admitted to link) |
| 3 | search_chat | `refuse_no_grounded_evidence` | `null` | — (refuses; no admitted evidence) |
| 4 | curation_compliance | `deny` | **`denied`** | `CUR-EXCLUDE-GSE301973` |

Print all stages of a scenario at once to compare during a run:

```bash
jq -r '.scenario_id, "final_outcome="+.final_outcome,
  (.stages[] | "  ["+(.order|tostring)+"] "+.stage+" decision="+.decision+" gate="+(.gate//"null")
   +" out="+(.output_entities|join(",")))' \
  09_decision_ground_truth/RKM-002.json
```

### Step 3 — Final scenario assertion

After the last stage, assert the rollup's `final_outcome` and HITL flag:

```bash
jq '{scenario_id, final_outcome, required_human_review}' 09_decision_ground_truth/RKM-004.json
# RKM-001 approved | RKM-002 needs_human_review (review=true) | RKM-003 approved_with_required_labeling | RKM-004 denied (review=true)
```

### Step 4 — Dataset self-check (run now, no agent runtime needed)

Validates the scenario plumbing itself — every produced entity resolves to a normalized
entity file, and each stage folder is wired for the chain:

```bash
cd dataset-seed
for f in 09_decision_ground_truth/RKM-*.json; do
  echo "## $f"
  jq -r '.stages[].output_entities[]' "$f" | sort -u | while read e; do
    [ -z "$e" ] && continue
    hit=$(find 01_* 02_* 03_* 04_* 05_* 06_* 07_* 08_* -name "$e.json" | head -1)
    [ -n "$hit" ] && echo "  ok   $e -> $hit" || echo "  MISS $e"
  done
done
```

Every line should read `ok` (no `MISS`). This confirms each scenario's expected outputs map
to real entities before you wire in a live agent runtime.

## How to trace a stage to raw files

```text
09_decision_ground_truth/RKM-XXX.json
  -> stages[].output_entities[]
  -> matching normalized entity in 01_* .. 08_*
  -> that entity's raw_sources[]
  -> concrete files under 00_raw/_corpus/  (also duplicated into the stage's input/ or expected_output/)
```

The canonical corpus in `00_raw/_corpus/` stays the single source of truth referenced by
`raw_manifest.json` and every normalized entity's `raw_sources`. The `RKM-XXX/` folders are
deterministic duplicates rebuilt by [`build_scenario_folders.py`](build_scenario_folders.py).

## Quick lookup

```bash
# list scenarios
ls dataset-seed/09_decision_ground_truth/RKM-*.json

# show a scenario's stages (agent / decision / gate)
jq '{scenario_id, path, final_outcome, stages: [.stages[] | {order, agent, decision, gate}]}' \
  dataset-seed/09_decision_ground_truth/RKM-002.json
```

## Regenerate

```bash
cd dataset-seed
python3 generate_raw_layer.py          # fetch/synthesize -> 00_raw/_corpus/  (needs network)
python3 generate_normalized_layers.py  # normalized entities + 09 RKM rollups (reads scenarios.py)
python3 generate_agent_documents.py    # multi-format replicas in _corpus/
python3 build_scenario_folders.py      # (offline) build 00_raw/RKM-*/ per-agent folders
```
