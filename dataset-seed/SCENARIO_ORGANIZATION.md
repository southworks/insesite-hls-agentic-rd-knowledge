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

The test cases are **end-to-end**: each scenario is one full path through the 4-agent workflow,
and the four differ at the human-in-the-loop gates. Each scenario folder has a sub-folder per
agent/stage so any agent can be started in isolation (see [TEST_CASES.md](TEST_CASES.md)).

```
00_raw/
  _corpus/                       ← CANONICAL: all source files + raw_manifest.json (single source of truth)
  RKM-001_full_approval/         ← e2e path: all gates Approved
  RKM-002_guardrail_review/      ← e2e path: deny/exclude → human review
  RKM-003_synthetic_provenance/  ← e2e path: approve_with_required_labeling
  RKM-004_curation_denied/       ← e2e path: blocked at curation gate

00_raw/RKM-001_full_approval/
  01_orchestrator/request.json
  02_ingestion_translation/  agent_input.json  input/  expected_output/
  03_metadata_linking/       agent_input.json  input/  expected_output/
  04_search_chat/            agent_input.json  input/  expected_output/
  05_curation_compliance/    agent_input.json  input/  expected_output/
  scenario.json              ← mirror of 09_decision_ground_truth/RKM-001.json
```

- `raw_manifest.json` and every normalized entity's `raw_sources` point into `_corpus/` (kept the
  single source of truth).
- The `RKM-*/` folders are rebuilt offline and deterministically by
  [`build_scenario_folders.py`](build_scenario_folders.py) from [`scenarios.py`](scenarios.py)
  (each stage's `input` = upstream docs/entities, `expected_output` = what it would produce).
- Cross-cutting multi-format `agent_inputs/` replicas stay in `_corpus/` (not per-scenario).

See [RAW_LAYER.md](RAW_LAYER.md), [TEST_CASES.md](TEST_CASES.md), and [HANDOFF.md](HANDOFF.md).

## Demo flow stories

Each story is one e2e scenario. The **Narrative** is a real-life, conversational walk-through of
how the data moves agent → agent; the bullets above it are the concrete demo handles.

### Story A — "Full approval" (`RKM-001_full_approval`)

- **Start anywhere:** point an agent at `00_raw/RKM-001_full_approval/<stage>/` — e.g.
  `04_search_chat/agent_input.json` carries the NL osimertinib query and `input/` holds the
  article (`PMC6889286`), trial (`NCT02296125`), GEO dataset (`GSE323366`) and Tagrisso label.
- **Flow:** ingestion accepts 5 OA articles → linking connects FLAURA ↔ `NDA208065` ↔ label →
  search answers with citations → curation approves. Every gate Approved.
- **Expected:** `09_decision_ground_truth/RKM-001.json` — `final_outcome: approved`.
- **Value:** the canonical happy path — clean ingestion all the way to a grounded, approved answer.

**Narrative (real-life data flow).** A translational oncology team is putting together the
first-line evidence base for osimertinib in EGFR-mutated NSCLC. A researcher asks the knowledge
hub: *"Pull together what we know about first-line osimertinib and the evidence behind it."* The
**orchestrator** reads that intent and fans the work out to the four agents. The **ingestion &
translation agent** reaches into the public portals listed in the partner/vendor manifest, pulls
five open-access review articles, and checks each one's reuse license; all five are clean CC-BY,
so it normalizes them into tidy `RDOC-*` records and hands that clean batch downstream. The
**metadata & linking agent** picks up those records plus the FLAURA / AURA3 trials and the
`NDA208065` regulatory application and label, notices they all talk about the *same* molecule
(osimertinib / Tagrisso / AZD9291), and stitches them into an evidence graph — emitting explicit
`LINK-*` edges and extracting the compound and target entities. A human glances at the proposed
links and approves them (first gate). Now the **search & chat agent** can actually answer the
researcher's question: it retrieves across the linked corpus and replies with citations to the
article, the trial, and the label, each traceable back to the raw file it came from. Finally the
**curation & compliance agent** reviews everything that was admitted, finds nothing sensitive, and
signs off. The researcher walks away with a cited answer *and* an auditable graph behind it.

### Story B — "Guardrail → human review" (`RKM-002_guardrail_review`)

- **Start here:** `00_raw/RKM-002_guardrail_review/` — same assembly but with a no-license
  article (`PMC4771182`) and patient-derived GEO (`GSE297057`, `GSE301973`) in the candidate pool.
- **Flow:** ingestion denies the bad-license article → linking excludes the patient-derived
  datasets → curation flags the exclusions and routes to a human.
- **Expected:** `09_decision_ground_truth/RKM-002.json` — `final_outcome: needs_human_review`,
  curation gate `denied_pending_human_review`.
- **Value:** exercises the deny/exclude branch and the HITL email gate of the diagram.

**Narrative (real-life data flow).** Same goal as Story A, but this time the candidate pool came
from a broad, messy portal sweep — and not everything in it is safe to keep. The **ingestion
agent** runs its license check and hits `PMC4771182`: the PMC OA API reports its license as
*none*, so the agent refuses to store the full text and keeps metadata only, recording an explicit
deny. The clean articles still pass. When the **metadata & linking agent** looks at the candidate
GEO datasets, it inspects the sample titles and realizes two of them — `GSE297057` (patient FFPE
specimens) and `GSE301973` (before/after treatment specimens) — are patient-derived, so it
excludes them and only lets the cell-line datasets through. The **search agent** answers the
question, but deliberately only over the *admitted* corpus, so the excluded sources never leak
into a citation. Then the **curation & compliance agent** gathers the three exclusions (one
licensing, two PHI), flags them as compliance-relevant, and — because these are exactly the kind
of calls a human should confirm — does *not* auto-approve. It routes the case to a reviewer by
email and parks the run at the human-in-the-loop gate. The story shows guardrails firing
mid-pipeline and the data handoff pausing for human judgment instead of silently proceeding.

### Story C — "Synthetic provenance → approve with labeling" (`RKM-003_synthetic_provenance`)

- **Start here:** `00_raw/RKM-003_synthetic_provenance/` — synthetic ELN/LIMS sample manifest,
  notebook, and QC report (`02_ingestion_translation/input/`) derived from public GEO structure.
- **Flow:** ingestion normalizes the synthetic records (stamping provenance) → linking associates
  each synthetic sample to its public GEO series → search can answer about them → curation approves
  *with required labeling*.
- **Expected:** `09_decision_ground_truth/RKM-003.json` — `final_outcome:
  approved_with_required_labeling`, provenance `synthetic_from_public_structure`.
- **Value:** shows how lab-style operational records enter the hub without implying patient data,
  and how a provenance label is enforced and travels with the data.

**Narrative (real-life data flow).** A lab has its own internal ELN/LIMS paperwork — a sample
manifest, an experiment notebook, a QC report — for a PC9/H1650 EGFR-inhibition study. These were
generated from the public GEO structure and contain *no* real patient data, but they look exactly
like the kind of operational records that usually would. The lab wants them searchable in the
knowledge hub without anyone ever mistaking them for clinical specimens. The **ingestion agent**
reads the CSV/TXT records and normalizes them into `SYN-LIMS-*` entities, and — critically —
stamps every one with the provenance tag `synthetic_from_public_structure` so the "this is
synthetic" fact is baked in from the first hop. The **metadata & linking agent** then ties each
synthetic sample back to the real public series it was modeled on (`SYN-LIMS-001` → `GSE323366`),
so the lineage is explicit. The **search agent** can now field a question like *"which synthetic
samples map to the PC9/H1650 series, and what's their provenance?"* and answer with citations that
state the synthetic origin out loud. The **curation & compliance agent** applies the data-handling
policy and reaches a nuanced verdict: approve — but *with required labeling*. The records are
admitted on the condition that the synthetic-provenance label stays attached wherever they go. The
story is about metadata (provenance) being a first-class part of the handoff, not an afterthought.

### Story D — "Sensitive content blocked → denied" (`RKM-004_curation_denied`)

- **Start here:** `00_raw/RKM-004_curation_denied/` — a single patient-derived GEO candidate
  (`GSE301973`), ingested as metadata only.
- **Flow:** ingestion defers the admissibility call → linking has nothing to link → search refuses
  (no grounded evidence) → curation denies and requires human review.
- **Expected:** `09_decision_ground_truth/RKM-004.json` — `final_outcome: denied`, curation gate
  `denied`.
- **Value:** the hard-stop path — compliance blocks content end-to-end and it never enters the base.

**Narrative (real-life data flow).** Someone proposes adding `GSE301973` to the knowledge base — a
GEO dataset whose sample titles reference before/after *treatment* specimens, i.e. patient-derived
material. The **orchestrator** routes the candidate in. The **ingestion agent** connects to the
portal but pulls only the metadata; it does *not* store any patient-level payload and deliberately
defers the real admissibility decision to curation rather than making it alone. With nothing
admitted, the **metadata & linking agent** has no entities to link, so it's a clean no-op — the
pipeline doesn't manufacture connections to content that hasn't cleared compliance. When the
**search agent** is asked to summarize the dataset, it refuses: there is no grounded, admitted
evidence it could cite, and answering from un-vetted patient data is exactly what the system is
built to avoid. Finally the **curation & compliance agent** makes the explicit call — the sample
titles reference patient specimens, so it *denies* the candidate, flags it as sensitive, and marks
it for human review. The final gate is **Denied**; `GSE301973` never becomes part of the knowledge
base. This is the mirror image of Story A: instead of clean data flowing all the way to an
approved answer, unsafe data is stopped cold and the trail of *why* is captured at every hop.

## Reproduce

```bash
cd dataset-seed
python3 generate_raw_layer.py          # fetch/synthesize -> 00_raw/_corpus/  (needs network)
python3 generate_normalized_layers.py  # normalized entities + 09 RKM rollups (reads scenarios.py)
python3 generate_agent_documents.py    # multi-format replicas in _corpus/
python3 build_scenario_folders.py      # (offline) rebuild 00_raw/RKM-*/ from _corpus/ + entities
```
