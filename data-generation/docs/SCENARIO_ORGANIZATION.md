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

HLS is **two sequential phases**, each closed by a distinct human actor (see
[HANDOFF.md](HANDOFF.md) and `../workflow-summary.md`): **phase 1 — ingestion** (`ING-*`, load
knowledge) and **phase 2 — search** (`QRY-*`, query it later). The demo traverses *every* agent and
both human actors, but only the **data-consuming** agents (+ the response) get a materialized,
self-contained folder; the human approvals and persistence are memory stages in `scenario.json`.

```
00_raw/
  _corpus/                          ← CANONICAL: all source files + raw_manifest.json (single source of truth)
  DEMO_SCENARIO/                    ← headline stateful demo, numbered in run order:
    1-QRY-001_no_data/                  search empty KB → no grounded answer
    2-ING-001_full_approval/            ingest: clean upload, curator approves, persisted
    3-QRY-002_grounded/                 search populated KB → grounded answer with citations
  ING-002_guardrail_review/         ← standalone: deny/exclude → curator review (nothing persisted)
  ING-003_synthetic_provenance/     ← standalone: approve_with_required_labeling
  ING-004_sensitive_blocked/        ← standalone: blocked at the curator gate

00_raw/DEMO_SCENARIO/2-ING-001_full_approval/   00_raw/DEMO_SCENARIO/3-QRY-002_grounded/
  01_ingestion_translation/  (agent stage)        01_search_chat/          (agent stage)
  02_metadata_linking/       (agent stage)        02_curation_compliance/  (agent stage)
  scenario.json                                   03_response/  response.json
  # curator approval + persistence:               scenario.json
  #   memory stages in scenario.json              # compliance approval: memory stage
  ← scenario.json mirrors 09_decision_ground_truth/<ID>.json
```

- `raw_manifest.json` and every normalized entity's `raw_sources` point into `_corpus/`; the root
  catalog `01_*..07_*` is the canonical entity set (both single sources of truth).
- The scenario folders are rebuilt offline and deterministically by
  [`build_scenario_folders.py`](build_scenario_folders.py) from [`scenarios.py`](scenarios.py)
  (each agent stage's `input` = upstream docs/entities, `expected_output` = what it would produce).
- Cross-cutting multi-format `agent_inputs/` replicas stay in `_corpus/` (not per-scenario).

See [RAW_LAYER.md](RAW_LAYER.md), [TEST_CASES.md](TEST_CASES.md), [HANDOFF.md](HANDOFF.md), and the
plain-language [TESTING_GUIDE.md](TESTING_GUIDE.md).

## Demo flow stories

Two phases, run at different times (immediate or deferred). The **Narrative** is a real-life,
conversational walk-through of how the data moves step → step; the bullets above it are the concrete
demo handles. The headline three are bundled under `00_raw/DEMO_SCENARIO/` in run order.

### ⭐ The headline demo — "search empty → ingest → search again"

Run three scenarios in sequence to show the knowledge base go from empty to answering:

1. **`DEMO_SCENARIO/1-QRY-001_no_data`** — ask the osimertinib question against an **empty** KB →
   *"no grounded information yet."*
2. **`DEMO_SCENARIO/2-ING-001_full_approval`** — upload + ingest the five articles → **persisted** into the KB.
3. **`DEMO_SCENARIO/3-QRY-002_grounded`** — ask the **same** question → a grounded answer with citations now appears.

Same question, two results — because data was loaded in between. That contrast is the demo.

---

## Phase 1 — Ingestion stories

### Story A — "Full approval" (`2-ING-001_full_approval`)

- **Start anywhere:** point a step at `00_raw/DEMO_SCENARIO/2-ING-001_full_approval/<stage>/` — e.g.
  `01_ingestion_translation/input/` holds the five uploaded open-access articles as raw `xml/`+`json/`.
- **Flow:** upload → ingestion accepts 5 OA articles → linking connects FLAURA ↔ `NDA208065` ↔ label
  → human approves → entities persisted into the KB.
- **Expected:** `09_decision_ground_truth/ING-001.json` — `final_outcome: approved_persisted`.
- **Value:** the canonical load — clean upload all the way to persisted, searchable knowledge.

**Narrative (real-life data flow).** A translational oncology team wants the first-line osimertinib
evidence available in the knowledge hub. A data steward clicks **"Upload & ingest"** and drops in
five open-access review articles — these uploaded files stand in for material that could come from
external medical portals. The **ingestion & translation agent** license-checks each one; all five
are clean CC-BY, so it normalizes them into tidy `RDOC-*` records. The **metadata & linking agent**
picks up those records plus the FLAURA / AURA3 trials and the `NDA208065` application and label,
notices they all describe the *same* molecule (osimertinib / Tagrisso / AZD9291), and stitches them
into an evidence graph — emitting `LINK-*` edges and extracting the compound and target. A human
reviews the batch and **approves** it, and it's **persisted** into the CMS/knowledge base. Nothing
has been *queried* yet — this flow's whole job is to make the knowledge exist and be trustworthy.

### Story B — "Guardrail → human review" (`ING-002_guardrail_review`)

- **Start here:** `00_raw/ING-002_guardrail_review/` — same load but with a no-license article
  (`PMC4771182`) and patient-derived GEO (`GSE297057`, `GSE301973`) in the uploaded pool.
- **Flow:** ingestion denies the bad-license article → linking excludes the patient-derived datasets
  → the human-approval gate holds the batch for review; nothing is persisted.
- **Expected:** `09_decision_ground_truth/ING-002.json` — `final_outcome: needs_human_review`,
  approval gate `denied_pending_human_review`.
- **Value:** the guardrail branch — license/PHI screening, then a human checkpoint before persistence.

**Narrative (real-life data flow).** Same goal, messier upload. The **ingestion agent** runs its
license check and hits `PMC4771182`: the PMC OA API reports its license as *none*, so it refuses to
store the full text and keeps metadata only, recording an explicit deny. The clean articles still
pass. The **metadata & linking agent** inspects the candidate GEO sample titles and finds two
patient-derived sets — `GSE297057` (FFPE specimens) and `GSE301973` (before/after treatment
specimens) — and excludes them, letting only the cell-line datasets through. At the **human-approval
gate**, the reviewer is shown the three exclusions and the batch is **held for review** rather than
auto-approved — so nothing is persisted until a person signs off. Guardrails fire during ingestion,
and persistence waits for human judgment.

### Story C — "Synthetic provenance → approve with labeling" (`ING-003_synthetic_provenance`)

- **Start here:** `00_raw/ING-003_synthetic_provenance/01_ingestion_translation/input/` — synthetic
  ELN/LIMS sample manifest, notebook, and QC report derived from public GEO structure.
- **Flow:** ingestion normalizes the synthetic records (stamping provenance) → linking associates
  each sample to its public GEO series → human approves *with required labeling* → persisted.
- **Expected:** `09_decision_ground_truth/ING-003.json` — `final_outcome:
  approved_with_required_labeling`, provenance `synthetic_from_public_structure`.
- **Value:** lab-style operational records enter the KB without implying patient data; the provenance
  label travels with them.

**Narrative (real-life data flow).** A lab uploads its internal ELN/LIMS paperwork for a PC9/H1650
EGFR-inhibition study — generated from public GEO structure, containing *no* real patient data, but
looking exactly like records that usually would. The **ingestion agent** normalizes them into
`SYN-LIMS-*` entities and stamps each with `synthetic_from_public_structure` so the "this is
synthetic" fact is baked in from the first hop. The **metadata & linking agent** ties each sample
back to the real public series it was modeled on (`SYN-LIMS-001` → `GSE323366`). At the gate, the
human **approves with a required-labeling condition**, and the records are persisted carrying that
label — so nobody downstream mistakes synthetic data for real patient data.

### Story D — "Sensitive content blocked → denied" (`ING-004_sensitive_blocked`)

- **Start here:** `00_raw/ING-004_sensitive_blocked/` — a single patient-derived GEO candidate
  (`GSE301973`), ingested as metadata only.
- **Flow:** ingestion defers the call → linking has nothing to link → the human-approval gate denies
  → nothing is persisted.
- **Expected:** `09_decision_ground_truth/ING-004.json` — `final_outcome: denied_not_persisted`,
  approval gate `denied`.
- **Value:** the hard-stop — compliance blocks content before it ever enters the KB.

**Narrative (real-life data flow).** Someone tries to upload `GSE301973`, a GEO dataset whose sample
titles reference before/after *treatment* specimens — patient-derived material. The **ingestion
agent** recognizes the risk and stores **metadata only**, deliberately deferring the admissibility
call rather than making it alone. With nothing admitted, the **metadata & linking agent** has
nothing to link — a clean no-op. At the **human-approval gate** the reviewer **denies** the
candidate; it never becomes part of the knowledge base. The mirror image of Story A: unsafe data is
stopped cold and the trail of *why* is captured at every step.

## Phase 2 — Search stories

### Story E — "Empty knowledge base" (`1-QRY-001_no_data`)

- **Start here:** `00_raw/DEMO_SCENARIO/1-QRY-001_no_data/` — the osimertinib query against an
  **empty** KB (`01_search_chat/input/` holds only `prompt.txt`; no entities).
- **Flow:** query → search finds nothing to ground on → compliance confirms a safe "no data" reply →
  response returned.
- **Expected:** `09_decision_ground_truth/QRY-001.json` — `final_outcome: no_grounded_answer`,
  `kb_state: empty`.
- **Value:** the *before* state of the headline demo.

**Narrative (real-life data flow).** A clinician-researcher opens the search console and clicks
**"Run query"** with the preset osimertinib question — but **nothing has been ingested yet**. The
**search & chat agent** has an empty knowledge base to retrieve from, so it can't ground an answer
and returns `no_grounded_answer`. The **curation & compliance agent** reviews that draft and
confirms the safe, honest response is to say *"no grounded information is available yet."* The system
refuses to invent an answer it can't cite.

### Story F — "Populated knowledge base" (`3-QRY-002_grounded`)

- **Start here:** `00_raw/DEMO_SCENARIO/3-QRY-002_grounded/` — the **same** query, now against a
  **populated** KB (`01_search_chat/input/` holds `prompt.txt` + the persisted article, trial, dataset, label).
- **Flow:** query → search retrieves the persisted evidence and drafts an answer with citations →
  compliance reviews it clean → response returned.
- **Expected:** `09_decision_ground_truth/QRY-002.json` — `final_outcome: answer_with_citations`,
  `kb_state: populated`.
- **Value:** the *after* state of the headline demo.

**Narrative (real-life data flow).** After `ING-001` persisted the evidence, the same researcher
runs the **same** query again. This time the **search & chat agent** retrieves the persisted
article, trial, dataset and label, and drafts an answer about osimertinib resistance and its
first-line evidence — with **≥2 citations and a raw-source trace** back to the original files. The
**curation & compliance agent** reviews the draft, finds no sensitive content, and **approves** it
for return. The grounded answer is delivered. Nothing about the search flow re-ran ingestion — it
simply queried knowledge that was already there.

## Reproduce

```bash
cd dataset-seed
python3 generate_raw_layer.py          # fetch/synthesize -> 00_raw/_corpus/  (needs network)
python3 generate_normalized_layers.py  # trimmed root catalog + 09 ING/QRY rollups (reads scenarios.py)
python3 generate_agent_documents.py    # multi-format replicas in _corpus/
python3 build_scenario_folders.py      # (offline) rebuild 00_raw/DEMO_SCENARIO/ + ING-* from catalog + _corpus/
```
