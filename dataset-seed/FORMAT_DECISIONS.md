# Format Decisions - HLS Agent Input Replicas

This document explains why the HLS scenario uses a scoped multi-format replica
set instead of mirroring the FSI and retail `generate_agent_documents.py`
behavior exactly.

## Decision Summary

| Source type | Existing Raw formats | Replica formats | Decision |
| --- | --- | --- | --- |
| Research articles | XML, JSON, license XML | TXT, MD, HTML, PDF evidence cards | Keep full article XML as source truth; replicate extractable metadata and abstract-level facts. |
| Clinical trials | JSON, protocol PDF, SAP PDF | TXT, MD, HTML, PDF evidence cards | Keep official protocol/SAP PDFs; replicate registry facts for format consistency testing. |
| Experimental datasets | JSON, SOFT TXT | TXT, MD, HTML, PDF evidence cards | Replicate dataset-level facts, not every sample row. |
| Regulatory | JSON, HTML, PDF | TXT, MD, HTML, PDF evidence cards | Keep official source docs; replicate application/label/source metadata. |
| Policy | HTML + source records | TXT, MD, HTML, PDF evidence cards | Replicate curated policy rules used by RAG/compliance agents. |
| Curation decisions | JSON | TXT, MD, HTML, PDF evidence cards | Replicate approve/deny decisions for auditability tests. |
| ELN/LIMS | TXT, CSV + normalized synthetic records | TXT, MD, HTML, PDF digest | Produce a synthetic digest; keep records clearly labeled synthetic. |

## Why Not Replicate Everything?

The HLS Raw Layer already exercises multiple ingestion paths:

- XML article full text,
- JSON APIs,
- official PDF protocol/SAP/review documents,
- HTML policy/regulatory pages,
- TXT and CSV synthetic operational records.

Generating full PDF/PNG copies of every raw source would be expensive, noisy,
and less realistic than the public-source originals. The useful validation
target is narrower: can the agents extract the same canonical facts from a
compact evidence card regardless of whether it is TXT, Markdown, HTML, or PDF?

## Why No PNG?

The FSI scenario needs PNG because borrower documents are often physical scans
or phone photos. The retail scenario has a believable PNG equivalent for
warehouse packing slips.

For this HLS scenario, the selected sources are digital research and regulatory
documents. Generating fake scanned patient, protocol, or lab images would blur
the compliance boundary and imply a source modality we do not actually have.

So the first HLS replica set intentionally does **not** create PNG files.
If a later demo specifically needs vision/OCR, the safer option is to create a
small, clearly synthetic scanned ELN page only, not scan-like patient or trial
records.

## Implementation

`generate_agent_documents.py` has no third-party dependencies. PDF files are
written with a small deterministic text-PDF writer, avoiding ReportLab/Pillow
requirements for this repo.

Generated agent inputs are written as first-level format folders in the Raw
Layer:

```text
00_raw/_corpus/txt/agent_inputs/<category>/<document_id>.txt
00_raw/_corpus/md/agent_inputs/<category>/<document_id>.md
00_raw/_corpus/html/agent_inputs/<category>/<document_id>.html
00_raw/_corpus/pdf/agent_inputs/<category>/<document_id>.pdf
```

This mirrors the existing scenario convention where alternate document formats
live directly under `00_raw/<format>/...`. The `agent_inputs` level keeps
format replicas separate from public source files of the same format.

The script reads normalized entities generated from the Raw Layer:

- `01_research_documents`
- `02_clinical_trials`
- `03_experimental_datasets`
- `05_regulatory_submissions`
- `06_policy_rag`
- `08_curation_decisions`

It also creates one synthetic ELN/LIMS digest from `SYN-LIMS-*` records.

## Regeneration Order

```bash
cd dataset-seed
python3 generate_raw_layer.py
python3 generate_normalized_layers.py
python3 generate_agent_documents.py
```

Run the agent document generator after any change to the Raw Layer or normalized
entity mappings.
