# Demo Runbook — running each flow by injecting the prepared documents

A high-level guide for the development team to **drive a live demo** of the R&D knowledge-mining
agents using the documents already prepared under `00_raw/{ING,QRY}-XXX_<path>/`. You don't need to
generate anything (the dataset ships ready) or run any terminal commands. Each scenario is a folder
you feed to the agents, one stage at a time, and watch it produce the expected result.

HLS is **two separate processes** (decoupled in time), each started by a **controlled UI action**
(a button/process trigger — not a free-form chatbot):

- **Ingestion flow** (`ING-*`) — *load* knowledge: `upload → ingestion/translation → metadata
  linking → human approval → persistence`. In the demo, **manual file upload** stands in for the
  "Connect Portals" connector (the uploaded files represent data that could come from external
  medical portals).
- **Search flow** (`QRY-*`) — *query* that knowledge: `query → search/chat → curation/compliance
  review → response`. Runs later, **against what ingestion already persisted**.

The whole dataset is anchored on one topic — **osimertinib / Tagrisso (AZD9291) in EGFR-mutated
NSCLC** — so everything operates over the same body of evidence. See [TEST_CASES.md](TEST_CASES.md)
for the case index and [HANDOFF.md](HANDOFF.md) for the precise handoff map.

## ⭐ The headline demo — "search empty → ingest → search again"

The most convincing run is three scenarios in sequence, showing the KB go from empty to answering:

1. **Search with no data** — run **`QRY-001_no_data`**. Ask the osimertinib question against an
   **empty** knowledge base. The search agent finds nothing to ground on; the response is
   *"No grounded information is available yet."* (final outcome `no_grounded_answer`).
2. **Ingest data** — run **`ING-001_full_approval`**. Upload the five open-access articles; they're
   license-checked, linked, approved by a human, and **persisted** into the knowledge base.
3. **Search again** — run **`QRY-002_grounded`**. Ask the *same* question. Now the search agent
   retrieves the freshly persisted evidence and answers with **≥2 citations + a raw-source trace**
   (final outcome `answer_with_citations`).

Same question, two different results — because data was loaded in between. That contrast is the demo.

## How a scenario folder is laid out

Every scenario lives in one folder with one sub-folder per stage, in run order. Each stage's
**primary file** is named by what it is:

```
INGESTION  00_raw/ING-001_full_approval/        SEARCH  00_raw/QRY-002_grounded/
  01_upload/         trigger.json                  01_query/          trigger.json
  02_ingestion_translation/  ┐                     02_search_chat/          ┐ agent stage:
  03_metadata_linking/       │ agent stage:        03_curation_compliance/  ┘  agent_input.json
    agent_input.json         │  agent_input.json   04_response/       response.json
    input/  expected_output/ ┘  input/             scenario.json   ← the answer key
  04_human_approval/  gate.json
  05_persistence/     persisted.json
  scenario.json   ← the full expected end-to-end result (the answer key)
```

- **trigger.json** — the controlled UI action that starts the flow (upload / query).
- **agent stages** carry `agent_input.json` (the payload that STARTS the agent), `input/` (the docs
  to FEED it) and `expected_output/` (what it SHOULD produce).
- **gate.json** — the human-approval decision. **persisted.json** — what lands in the KB.
  **response.json** — what the search flow returns.

## How to run a scenario

For each stage, in order:

1. **Inject** the agent with that stage's `agent_input.json` and the files in its `input/` folder.
2. **Observe** what the agent does — the runbook below tells you what to expect in plain terms.
3. **Compare** the agent's result against that stage's `expected_output/` — same decision, same
   entities, same gate.
4. **Hand off** to the next stage: a stage's `expected_output/` is the next stage's `input/`.

> **Start anywhere.** You don't have to start at stage 1. To open mid-flow (e.g. straight at Search
> & Chat against a populated KB), just inject that stage's `input/` — it already contains the
> entities the upstream steps *would* have produced.

> **The gates.** Human-in-the-loop shows up as the `gate` field (`approved`, `denied`,
> `denied_pending_human_review`, `approved_with_labeling`). The scenarios differ precisely at the
> gates (ingestion) and the KB state (search).

---

# Ingestion flow scenarios

## ING-001 — Full approval *(clean upload, persisted)*

**The situation.** A researcher uploads the first-line osimertinib papers. Everything is clean,
open-access, and compliant — the "everything works" load.

**The data flow, in plain terms.** The uploaded files stand in for data that could come from
external portals. The hub license-checks five open-access papers and normalizes them, connects the
dots between the FLAURA trial, the FDA approval (NDA 208065) and the Tagrisso label, and extracts
the drug (osimertinib) and target (EGFR). A human approves the batch, and it's **persisted** into
the knowledge base — ready to be searched later.

**Step by step:**

1. **Upload** — `01_upload/trigger.json` is the "Upload & ingest" action listing the 5 PMCIDs; the
   raw articles are in `01_upload/input/`.
2. **Ingestion & Translation** → inject `02_ingestion_translation/input/` (5 articles as raw
   `xml/`+`json/`). The agent license-checks and normalizes them. *Result:* `…/expected_output/` —
   5× `RDOC-*`, decision `approve`. → next.
3. **Metadata & Linking** → inject `03_metadata_linking/input/` (trial + regulatory entities). Links
   **FLAURA ↔ NDA 208065** and **NDA 208065 ↔ Tagrisso label**, extracts `CMP-CHEMBL3353410` +
   `TGT-CHEMBL203`. *Result:* `03_metadata_linking/expected_output/`. → next.
4. **Human approval** — `04_human_approval/gate.json`: the reviewer **approves** the batch (gate `approved`).
5. **Persistence** — `05_persistence/persisted.json`: the 5 articles + 2 links + 2 extracted
   entities are **persisted into the CMS/knowledge base**.

**Final outcome:** `approved_persisted` — the KB now holds this evidence. (Matches `scenario.json`.)

## ING-002 — License & PHI guardrail *(routed to human review)*

**The situation.** A messier upload: one paper has no usable license, and two GEO datasets are
patient-derived. The guardrails catch them and escalate to a human before anything is persisted.

**The data flow, in plain terms.** During ingestion the hub denies the no-license paper
(`PMC4771182`) and keeps metadata only. During linking it excludes two patient-derived datasets
(`GSE297057`, `GSE301973`). At the approval gate, the human reviewer is shown the three exclusions
and the batch is **held for human review** — nothing is persisted yet.

**Step by step:**

1. **Upload** — `01_upload/trigger.json` lists 6 PMCIDs (incl. `PMC4771182`) and 7 dataset ids.
2. **Ingestion & Translation** → accepts 5 articles, **denies the no-license one**. *Result:*
   5× `RDOC-*` **+ `CUR-EXCLUDE-PMC4771182`**, decision `approve_with_exclusions`. → next.
3. **Metadata & Linking** → admits the 5 cell-line datasets, **excludes the 2 patient-derived**.
   *Result:* `DATASET-GSE*` **+ `CUR-EXCLUDE-GSE297057`, `CUR-EXCLUDE-GSE301973`**. → next.
4. **Human approval** — `gate.json`: reviewer is shown the 3 exclusions and the batch is **flagged
   for human review** (gate **`denied_pending_human_review`**).
5. **Persistence** — `persisted.json`: status `pending_human_review`, **nothing persisted**.

**Final outcome:** `needs_human_review` — held at the approval gate.

## ING-003 — Synthetic ELN/LIMS provenance *(approve with required labeling)*

**The situation.** The uploaded material is **synthetic** lab data (ELN/LIMS generated from public
structure). Usable, but only if clearly labeled as synthetic.

**The data flow, in plain terms.** The hub ingests two synthetic lab samples, stamping their
provenance as `synthetic_from_public_structure`, and maps them to the real public GEO series they
were modeled on. The human approves them **with a required-labeling condition**, and they're
persisted carrying that label so nobody mistakes synthetic data for real patient data.

**Step by step:**

1. **Upload** — `01_upload/trigger.json` lists the synthetic ELN/LIMS files (in `01_upload/input/`).
2. **Ingestion & Translation** → normalizes them with `synthetic_from_public_structure` provenance.
   *Result:* `SYN-LIMS-001`, `SYN-LIMS-010`, decision `approve`. → next.
3. **Metadata & Linking** → maps both samples to **`DATASET-GSE323366`**. → next.
4. **Human approval** — `gate.json`: **approve with required labeling** (gate **`approved_with_labeling`**).
5. **Persistence** — `persisted.json`: persisted with `required_label: synthetic_from_public_structure`.

**Final outcome:** `approved_with_required_labeling` — usable, but must carry the synthetic label.

## ING-004 — Sensitive content blocked *(denied, never persisted)*

**The situation.** A single uploaded candidate (`GSE301973`) is patient-derived (before/after
treatment specimens). It must be blocked, and nothing is persisted.

**The data flow, in plain terms.** The hub recognizes the candidate as patient-derived and stores
**metadata only**, deferring the call to the human. With nothing admitted, linking is a no-op. At
the approval gate the reviewer **denies** the candidate; it never enters the knowledge base.

**Step by step:**

1. **Upload** — `01_upload/trigger.json` lists `GSE301973` and flags it patient-derived.
2. **Ingestion & Translation** → stores **metadata only**, decision `defer_to_human_approval`. → next.
3. **Metadata & Linking** → **no action** (nothing admitted). → next.
4. **Human approval** — `gate.json`: reviewer **denies** (reason `patient_derived_specimens`, gate **`denied`**).
5. **Persistence** — `persisted.json`: status `denied_not_persisted`, **nothing persisted**.

**Final outcome:** `denied_not_persisted` — patient-derived content blocked before the KB.

---

# Search flow scenarios

## QRY-001 — Empty knowledge base *(no grounded answer — demo step 1)*

**The situation.** A user asks the osimertinib question, but **nothing has been ingested yet**.

**The data flow, in plain terms.** The search agent has an empty KB to retrieve from, so it can't
ground an answer. The compliance review confirms that returning a "no data yet" message is the safe,
correct response. This is the *before* state of the headline demo.

**Step by step:**

1. **Query** — `01_query/trigger.json`: the "Run query" action with the preset question and `kb_state: empty`.
2. **Search & Chat** → inject `02_search_chat/input/` (**empty** — nothing in the KB). The agent
   returns `no_grounded_answer` (`reason: knowledge_base_empty`).
3. **Curation & Compliance** → reviews the draft and **confirms the no-data response** (gate `approved`).
4. **Response** — `04_response/response.json`: *"No grounded information is available yet — ingest knowledge first."*

**Final outcome:** `no_grounded_answer`.

## QRY-002 — Populated knowledge base *(grounded answer — demo step 3)*

**The situation.** The **same** question, but now run **after `ING-001` persisted** the evidence.

**The data flow, in plain terms.** The search agent retrieves the persisted article, trial, dataset
and label, and drafts an answer with citations and a raw-source trace. The compliance review finds
no sensitive content and approves it. The grounded answer is returned — the *after* state of the demo.

**Step by step:**

1. **Query** — `01_query/trigger.json`: the same preset question, `kb_state: populated`
   (`depends_on_ingestion: ING-001`).
2. **Search & Chat** → inject `02_search_chat/input/` (the persisted article, trial, dataset, label).
   The agent answers with **≥2 citations + raw-source trace**. *Result:* `…/expected_output/`
   (`expected_citations`, `expected_answer_points`). → next.
3. **Curation & Compliance** → inject `03_curation_compliance/input/` (the cited entities). The agent
   finds nothing sensitive and **approves the response** (gate `approved`).
4. **Response** — `04_response/response.json`: the grounded answer with its citations.

**Final outcome:** `answer_with_citations`.

---

## At-a-glance: what each scenario should end with

| Scenario | Flow | What it demonstrates | Final outcome | Human review? |
|---|---|---|---|---|
| `ING-001` | ingestion | clean open-access load, persisted | `approved_persisted` | no |
| `ING-002` | ingestion | license + PHI guardrails → exclusions escalated | `needs_human_review` | yes — approval gate |
| `ING-003` | ingestion | synthetic data with required provenance labeling | `approved_with_required_labeling` | no |
| `ING-004` | ingestion | patient-derived content blocked, never persisted | `denied_not_persisted` | yes — approval gate |
| `QRY-001` | search | query an empty KB | `no_grounded_answer` | no |
| `QRY-002` | search | query a populated KB | `answer_with_citations` | no |

Each scenario's `scenario.json` is the full answer key for that run. The headline demo is
**`QRY-001` → `ING-001` → `QRY-002`**.
