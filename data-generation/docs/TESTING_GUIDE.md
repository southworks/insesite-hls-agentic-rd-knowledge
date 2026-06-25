# Demo Runbook — running each phase by injecting the prepared documents

A high-level guide for the development team to **drive a live demo** of the R&D knowledge-mining
agents using the documents already prepared under `00_raw/`. You don't need to generate anything
(the dataset ships ready) or run any terminal commands.

HLS is **two sequential phases**, each closed by a **distinct human actor**, each started by a
**controlled UI action** (a button/process trigger — not a free-form chatbot):

- **Phase 1 — Ingestion & structuring** (`ING-*`): `ingestion & translation → metadata & linking →
  [knowledge curator approves] → persistence`. In the demo, **manual file upload** stands in for the
  "Connect Portals" connector.
- **Phase 2 — Search & compliance** (`QRY-*`): `search & chat → curation & compliance →
  [compliance owner approves] → response`. Runs after phase 1 — immediately or deferred.

The demo **walks every agent and both human actors**, but datasets are prepared **only for the
agents/actions that consume data** (ingestion & translation, metadata & linking, search & chat,
curation & compliance) plus the final response. The two human approvals and persistence are part of
the answer key (`scenario.json`) but have no folder — you narrate them.

Everything is anchored on one topic — **osimertinib / Tagrisso (AZD9291) in EGFR-mutated NSCLC** — so
both phases operate over the same body of evidence. See [TEST_CASES.md](TEST_CASES.md) for the case
index and [HANDOFF.md](HANDOFF.md) for the precise handoff map.

## ⭐ The headline demo — "search empty → ingest → search again"

The bundle in **`00_raw/DEMO_SCENARIO/`** is the headline run, numbered in execution order:

1. **`1-QRY-001_no_data`** — ask the osimertinib question against an **empty** KB. Search finds
   nothing to ground on; the response is *"No grounded information is available yet."*
   (`no_grounded_answer`).
2. **`2-ING-001_full_approval`** — upload the five open-access articles; they're license-checked,
   linked, **approved by the knowledge curator**, and **persisted** into the KB.
3. **`3-QRY-002_grounded`** — ask the *same* question. Now search retrieves the freshly persisted
   evidence and answers with **≥2 citations + a raw-source trace** (`answer_with_citations`).

Same question, two results — because data was loaded in between. That contrast is the demo. The
guardrail variants `ING-002/003/004` live standalone at the `00_raw/` root.

## How a scenario folder is laid out

Each scenario has one numbered sub-folder per **materialized** stage, in run order:

```
PHASE 1  00_raw/DEMO_SCENARIO/2-ING-001_full_approval/
  01_ingestion_translation/  ┐ agent stage:
  02_metadata_linking/       ┘  agent_input.json + input/ + expected_output/
  scenario.json   ← full answer key (incl. the curator approval + persistence we narrate)

PHASE 2  00_raw/DEMO_SCENARIO/3-QRY-002_grounded/
  01_search_chat/            ┐ agent stage:
  02_curation_compliance/    ┘  agent_input.json + input/ + expected_output/
  03_response/        response.json
  scenario.json   ← full answer key (incl. the compliance approval we narrate)
```

- **agent stages** carry `agent_input.json` (the payload that STARTS the agent), `input/` (the docs
  to FEED it) and `expected_output/` (what it SHOULD produce, incl. `_expected_output.json`).
- **`03_response/response.json`** — what the search phase returns.
- **The human approvals and persistence** are NOT folders — read them from `scenario.json` →
  `stages[]` (the entries with `materialized: false`).

## How to run a scenario

For each materialized stage, in order:

1. **Inject** the agent with that stage's `agent_input.json` and the files in its `input/` folder.
2. **Observe** what the agent does — the runbook below tells you what to expect in plain terms.
3. **Compare** the agent's result against that stage's `expected_output/` — same decision, same
   entities, same gate.
4. **Narrate the human gate** at the end of the phase (curator for phase 1, compliance owner for
   phase 2) from `scenario.json`, then hand off.

> **Start anywhere.** You don't have to start at stage 1. To open mid-phase (e.g. straight at Search
> & chat against a populated KB), just inject that stage's `input/` — it already contains the
> entities the upstream steps *would* have produced.

> **The gates.** Each phase ends with a human actor reviewing the **joint** output of its two
> agents. The decision shows up as the stage `gate` field (`approved`, `denied`,
> `denied_pending_human_review`, `approved_with_labeling`). Ingestion scenarios differ at the
> curator gate; search scenarios differ at the KB state.

---

# Phase 1 — Ingestion scenarios

## ING-001 — Full approval *(clean upload, persisted)* — demo step 2

**The situation.** A researcher uploads the first-line osimertinib papers. Everything is clean,
open-access, and compliant — the "everything works" load.

**The data flow, in plain terms.** The uploaded files stand in for data that could come from
external portals. The hub license-checks five open-access papers and normalizes them; metadata &
linking RAG-retrieves the FLAURA trial + the FDA approval (NDA 208065) + the Tagrisso label and
connects the dots, extracting the drug (osimertinib) and target (EGFR). The **knowledge curator**
approves the batch, and it's **persisted** into the knowledge base — ready to be searched later.

**Step by step** (`00_raw/DEMO_SCENARIO/2-ING-001_full_approval/`):

1. **Ingestion & translation** → inject `01_ingestion_translation/input/` (5 articles as raw
   `xml/`+`json/`). The agent license-checks and normalizes them. *Result:* `…/expected_output/` —
   5× `RDOC-*`, decision `approve`. → next.
2. **Metadata & linking** → inject `02_metadata_linking/input/` (the 5 `RDOC-*` from step 1). The
   agent RAG-retrieves the trial + regulatory entities, links **FLAURA ↔ NDA 208065** and
   **NDA 208065 ↔ Tagrisso label**, extracts `CMP-CHEMBL3353410` + `TGT-CHEMBL203`.
   *Result:* `02_metadata_linking/expected_output/`. → curator.
3. **Knowledge curator** *(narrate from `scenario.json`)* — reviews the joint batch and **approves**
   (gate `approved`).
4. **Persistence** *(narrate)* — the 5 articles + 2 links + 2 extracted entities are **persisted into
   the CMS/knowledge base**.

**Final outcome:** `approved_persisted` — the KB now holds this evidence.

## ING-002 — License & PHI guardrail *(routed to the curator)*

**The situation.** A messier upload: one paper has no usable license, and two GEO datasets are
patient-derived. The guardrails catch them and escalate to the curator before anything is persisted.

**Step by step** (`00_raw/ING-002_guardrail_review/`):

1. **Ingestion & translation** → accepts 5 articles, **denies the no-license one** (`PMC4771182`).
   *Result:* 5× `RDOC-*` **+ `CUR-EXCLUDE-PMC4771182`**, decision `approve_with_exclusions`. → next.
2. **Metadata & linking** → admits the 5 cell-line datasets, **excludes the 2 patient-derived**.
   *Result:* `DATASET-GSE*` **+ `CUR-EXCLUDE-GSE297057`, `CUR-EXCLUDE-GSE301973`**. → curator.
3. **Knowledge curator** *(narrate)* — shown the 3 exclusions, **flags the batch for human review**
   (gate **`denied_pending_human_review`**).
4. **Persistence** *(narrate)* — status `pending_human_review`, **nothing persisted**.

**Final outcome:** `needs_human_review`.

## ING-003 — Synthetic ELN/LIMS provenance *(approve with required labeling)*

**Step by step** (`00_raw/ING-003_synthetic_provenance/`):

1. **Ingestion & translation** → inject `01_ingestion_translation/input/` (synthetic ELN/LIMS csv+txt).
   Normalizes them with `synthetic_from_public_structure` provenance. *Result:* `SYN-LIMS-001`,
   `SYN-LIMS-010`, decision `approve`. → next.
2. **Metadata & linking** → maps both samples to **`DATASET-GSE323366`**. → curator.
3. **Knowledge curator** *(narrate)* — **approve with required labeling** (gate **`approved_with_labeling`**).
4. **Persistence** *(narrate)* — persisted with `required_label: synthetic_from_public_structure`.

**Final outcome:** `approved_with_required_labeling`.

## ING-004 — Sensitive content blocked *(denied, never persisted)*

**Step by step** (`00_raw/ING-004_sensitive_blocked/`):

1. **Ingestion & translation** → inject `01_ingestion_translation/input/` (the `GSE301973`
   curation-decision note). Stores **metadata only**, decision `defer_to_human_approval`. → next.
2. **Metadata & linking** → **no action** (nothing admitted; `input/` empty). → curator.
3. **Knowledge curator** *(narrate)* — **denies** (reason `patient_derived_specimens`, gate **`denied`**).
4. **Persistence** *(narrate)* — status `denied_not_persisted`, **nothing persisted**.

**Final outcome:** `denied_not_persisted`.

---

# Phase 2 — Search scenarios

## QRY-001 — Empty knowledge base *(no grounded answer — demo step 1)*

**The situation.** A user asks the osimertinib question, but **nothing has been ingested yet**.

**Step by step** (`00_raw/DEMO_SCENARIO/1-QRY-001_no_data/`):

1. **Search & chat** → inject `01_search_chat/input/` (**only `prompt.txt`** — nothing in the KB). The
   agent returns `no_grounded_answer` (`reason: knowledge_base_empty`). → next.
2. **Curation & compliance** → reviews the draft and **confirms the no-data response**
   (`02_curation_compliance/`). → compliance owner.
3. **Compliance owner** *(narrate)* — approves the safe "no data" reply (gate `approved`).
4. **Response** — `03_response/response.json`: *"No grounded information is available yet — ingest
   knowledge first."*

**Final outcome:** `no_grounded_answer`.

## QRY-002 — Populated knowledge base *(grounded answer — demo step 3)*

**The situation.** The **same** question, but now run **after `ING-001` persisted** the evidence.

**Step by step** (`00_raw/DEMO_SCENARIO/3-QRY-002_grounded/`):

1. **Search & chat** → inject `01_search_chat/input/` (`prompt.txt` + the persisted article, trial,
   dataset, label). The agent answers with **≥2 citations + raw-source trace**. *Result:*
   `…/expected_output/` (`expected_citations`, `expected_answer_points`). → next.
2. **Curation & compliance** → inject `02_curation_compliance/input/` (the cited entities). Finds
   nothing sensitive and **approves the response**. → compliance owner.
3. **Compliance owner** *(narrate)* — approves (gate `approved`).
4. **Response** — `03_response/response.json`: the grounded answer with its citations.

**Final outcome:** `answer_with_citations`.

---

## At-a-glance: what each scenario should end with

| Scenario | Phase | What it demonstrates | Final outcome | Human review? |
|---|---|---|---|---|
| `ING-001` | 1 | clean open-access load, persisted | `approved_persisted` | no (curator auto-approves) |
| `ING-002` | 1 | license + PHI guardrails → exclusions escalated | `needs_human_review` | yes — curator |
| `ING-003` | 1 | synthetic data with required provenance labeling | `approved_with_required_labeling` | no |
| `ING-004` | 1 | patient-derived content blocked, never persisted | `denied_not_persisted` | yes — curator |
| `QRY-001` | 2 | query an empty KB | `no_grounded_answer` | no |
| `QRY-002` | 2 | query a populated KB | `answer_with_citations` | no |

Each scenario's `scenario.json` is the full answer key for that run (including the human approvals
and persistence you narrate). The headline demo is **`1-QRY-001` → `2-ING-001` → `3-QRY-002`**.
