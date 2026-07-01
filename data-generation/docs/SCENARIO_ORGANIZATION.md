# Scenario Organization

Demo-first layout: each case is a self-contained folder under `rd-knowledge-mining/backend/dataset-seed/cases/`.

## Case folders

| Folder | Legacy ID | Role |
|--------|-----------|------|
| `case-01-human-review` | ING-002 | Guardrail review — human curator gate |
| `case-02-approval-labeling` | ING-003 | Synthetic ELN/LIMS with required labeling |
| `case-03-sensitive-denied` | ING-004 | Patient-derived candidate denied |
| `case-04-demo` | QRY-001, ING-001, QRY-002 | Stateful headline demo |

## case-04-demo structure

```
case-04-demo/
  ingest/           ING-001 upload (5 OA articles)
  prompts/
    01-no-data-prompt.txt      QRY-001
    03-grounded-query-prompt.txt   QRY-002
  README.md
```

## Ground truth

Optional validation keys in `data-generation/ground-truth/<ID>.json` mirror the full e2e chain including memory stages (gates, persistence).

## Rebuild

```bash
cd data-generation/scripts
python3 generate_raw_layer.py
python3 build_case_folders.py
```

Scenario definitions: [`scenarios.py`](../scripts/scenarios.py)

## How to add a scenario

Add the new `ING-XXX` or `QRY-XXX` definition in `data-generation/scripts/scenarios.py`, update the relevant folder or prompt mapping, run `build_case_folders.py`, review changes under `rd-knowledge-mining/backend/dataset-seed/`, and redeploy the assets that embed the dataset.
