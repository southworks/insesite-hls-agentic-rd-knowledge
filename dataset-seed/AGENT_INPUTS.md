# Agent Input Replicas - Multi-format Evidence Cards

The HLS Raw Layer already contains mixed public-source formats: XML, JSON, PDF,
HTML, TXT, and CSV. Full duplication of every raw source into every format would
add noise and make the dataset harder to inspect.

Instead, `generate_agent_documents.py` creates compact evidence cards from the
normalized entities that were derived from `00_raw/`. Each card preserves the
same canonical facts across four formats:

- TXT
- Markdown
- HTML
- PDF

These files live under first-level format folders in `00_raw/`, aligned with
the structure used by the FSI and retail scenario repos:

```text
00_raw/
|-- txt/agent_inputs/
|-- md/agent_inputs/
|-- html/agent_inputs/
|-- pdf/agent_inputs/
`-- agent_document_manifest.json
```

## Category Mapping

| Entity folder | Replicated documents | Output category |
| --- | ---: | --- |
| `01_research_documents/` | 5 | `research_documents` |
| `02_clinical_trials/` | 4 | `clinical_trials` |
| `03_experimental_datasets/DATASET-*` | 5 | `experimental_datasets` |
| `05_regulatory_submissions/` | 6 | `regulatory_submissions` |
| `06_policy_rag/` | 6 | `policy_rag` |
| `08_curation_decisions/` | 14 | `curation_decisions` |
| synthetic ELN/LIMS digest | 1 | `synthetic_eln_lims` |

Total: **41 evidence cards x 4 formats = 164 replica files**, plus
`agent_document_manifest.json`.

## Generate

```bash
cd dataset-seed
python3 generate_agent_documents.py
python3 generate_agent_documents.py --formats txt md html
python3 generate_agent_documents.py --categories clinical_trials regulatory_submissions
```

Recommended full regeneration order:

```bash
cd dataset-seed
python3 generate_raw_layer.py
python3 generate_normalized_layers.py
python3 generate_agent_documents.py
```

`generate_raw_layer.py` rebuilds `00_raw/` from scratch, so run
`generate_agent_documents.py` after it.

## How To Use In Tests

Use `00_raw/agent_document_manifest.json` to compare each `document_id` across
formats.

For a consistency check, an extraction pipeline should recover the same core
fields from:

```text
00_raw/txt/agent_inputs/<category>/<document_id>.txt
00_raw/md/agent_inputs/<category>/<document_id>.md
00_raw/html/agent_inputs/<category>/<document_id>.html
00_raw/pdf/agent_inputs/<category>/<document_id>.pdf
```

Expected stable fields include:

- document ID,
- source entity path,
- title or source ID,
- canonical IDs such as `NCT02296125`, `PMC6889286`, `GSE323366`,
  `CHEMBL3353410`, and `NDA208065`,
- policy refs and curation decisions where applicable,
- raw source trace.

## Relationship To Raw And Normalized Layers

```text
00_raw/ public/synthetic source files
  -> generate_normalized_layers.py
  -> 01_* ... 09_* normalized JSON entities
  -> generate_agent_documents.py
  -> 00_raw/{txt,md,html,pdf}/agent_inputs
```

The format replicas are not a new source of truth. They are test fixtures for
multi-format extraction consistency.
