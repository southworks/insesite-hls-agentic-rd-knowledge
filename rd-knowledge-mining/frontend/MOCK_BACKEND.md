# Mock Backend — Replacement Instructions

This document explains how the frontend currently uses **mocked data** and how to switch to a **real backend** once it is available.

## Current mock setup

When `UseMockBackend` is `true` (default in `appsettings.json` and `appsettings.Development.json`):

- `MockRdKnowledgeApiClient` implements `IRdKnowledgeApiClient`.
- `MockWorkflowSimulator` advances ingestion and curation runs in memory on each status poll.
- Block 2 uses **two processes**: Search & Chat (session-scoped messages) and Curate (on-demand curation execution).
- Scenario definitions and agent output JSON live under [`dataset-seed/`](dataset-seed/).
- `KnowledgeSessionStore` tracks open workspaces for the current Blazor circuit (browser session).
- Approving an ingestion run updates the in-memory Vector DB summary counts (simulates a Vector DB write).

The UI **never calls HTTP** for workflow operations while mocks are enabled. Razor components consume mapped DTOs only — they do not parse raw agent JSON.

## Configuration

```json
{
  "UseMockBackend": true,
  "ApiBaseUrl": "http://localhost:5038/",
  "DatasetSeed": { "RootPath": "../../dataset-seed" },
  "WorkflowPolling": {
    "IntervalSeconds": 2,
    "MaxDurationMinutes": 10
  }
}
```

| Setting | Mock mode | Production mode |
|---------|-----------|-----------------|
| `UseMockBackend` | `true` | `false` |
| `ApiBaseUrl` | Ignored | API FQDN |
| `DatasetSeed:RootPath` | Required for scenarios | Optional (catalog previews only) |

## When a backend is available

### 1. Set configuration

In `appsettings.Development.json`, Azure Container Apps env, or deployment pipeline:

```json
{
  "UseMockBackend": false,
  "ApiBaseUrl": "https://{your-api-host}/"
}
```

### 2. Implement `RdKnowledgeApiClient`

Replace any stub behavior in [`Services/RdKnowledgeApiClient.cs`](../src/WebApp/Services/RdKnowledgeApiClient.cs) with HTTP calls matching the routes documented in [`Contracts/Backend/RdKnowledgeBackendContracts.cs`](../src/WebApp/Contracts/Backend/RdKnowledgeBackendContracts.cs):

| Method | HTTP | Path |
|--------|------|------|
| Health | GET | `/health` |
| Start ingestion | POST | `/api/rd-knowledge/studies/{studyId}/ingestion/workflow/start` |
| Ingestion status | GET | `/api/rd-knowledge/executions/{executionId}/ingestion/status` |
| Ingestion HITL resume | POST | `/api/rd-knowledge/executions/{executionId}/ingestion/resume` |
| Start query workflow | POST | `/api/rd-knowledge/query/sessions/{sessionId}/workflow/start` |
| Get query session | GET | `/api/rd-knowledge/executions/{executionId}/query/session` |
| Send chat message | POST | `/api/rd-knowledge/executions/{executionId}/query/chat` |
| Start curation | POST | `/api/rd-knowledge/executions/{executionId}/query/curate` |
| Curation status | GET | `/api/rd-knowledge/executions/{executionId}/query/status` |
| Curation HITL resume | POST | `/api/rd-knowledge/executions/{executionId}/query/resume` |
| Study documents | GET | `/api/rd-knowledge/studies/{studyId}/documents` |
| Vector DB summary | GET | `/api/rd-knowledge/vector-db/summary` |

Use `ApiProblemDetails.EnsureSuccessOrThrowAsync` for failed responses. Map payloads through `BackendWorkflowMapper` — do not let Razor components parse raw JSON.

### 3. Verify DI registration

In [`Program.cs`](../src/WebApp/Program.cs):

```csharp
if (configuration.GetValue("UseMockBackend", true))
    builder.Services.AddSingleton<IRdKnowledgeApiClient, MockRdKnowledgeApiClient>();
else
    builder.Services.AddHttpClient<IRdKnowledgeApiClient, RdKnowledgeApiClient>(client =>
        client.BaseAddress = new Uri(configuration["ApiBaseUrl"]!));
```

No other registration changes should be required.

### 4. Remove or gate mock-only code (optional)

Once the backend is stable:

| File | Action |
|------|--------|
| `Services/MockRdKnowledgeApiClient.cs` | Delete, or keep behind `#if DEBUG` / feature flag for demos |
| `Services/MockWorkflowSimulator.cs` | Delete with mock client |
| `dataset-seed/studies/*/agent-outputs/` | Keep for catalog previews; stop using for workflow progression |
| In-memory Vector DB mutation in mock client | Replace with `GET .../vector-db/summary` from API |

### 5. Update tests

- Keep unit tests for `BackendWorkflowMapper`, `AgentOutputParser`, `ScenarioPickerFilter`, and workspace state classes using fixture JSON from `dataset-seed/`.
- Add `RdKnowledgeApiClientTests` with a mocked `HttpMessageHandler` (mirror the loan-mortgage demo pattern).
- Remove tests that assert mock tick progression if `MockWorkflowSimulator` is deleted.

### 6. Verify integration

- [ ] `GET /health` succeeds on frontend (`/health`) and API.
- [ ] Start ingestion run → poll → Vector DB summary reflects indexing after metadata linking → Knowledge Curator approve/deny closes run.
- [ ] Send chat messages **without** a fresh ingestion run → citations and lineage come from Vector DB via API.
- [ ] Curate on demand over accumulated chat → Compliance Reviewer HITL via API.
- [ ] Block 2 works independently (reads accumulated Vector DB data).
- [ ] Docker / Container Apps inject `ApiBaseUrl` and `UseMockBackend=false`.

### 7. Files that should NOT require UI changes

These consume DTOs from `IRdKnowledgeApiClient` only:

- `Components/Pages/KnowledgePortfolio.razor`
- `Components/Pages/IngestionWorkspace.razor`
- `Components/Pages/QueryWorkspace.razor`
- All agent panels, HITL panels, and workflow components

If backend payload shapes differ, update `Contracts/` and `BackendWorkflowMapper` — not Razor markup.

## Checklist for the replacing agent

- [ ] `UseMockBackend=false` in all deployed environments
- [ ] All `IRdKnowledgeApiClient` methods implemented over HTTP
- [ ] Contract namespaces unchanged (avoid breaking tests)
- [ ] Polling intervals unchanged unless backend recommends SSE/WebSockets later
- [ ] Mock services documented as demo-only or removed
- [ ] `rd-knowledge-mining/frontend/MOCK_BACKEND.md` updated if routes or config keys change

## Local development

```powershell
cd rd-knowledge-mining/frontend/src/WebApp
dotnet run
```

Open `http://localhost:5147`. With mocks enabled, no backend process is required.

To test against a local API:

1. Start the backend on port 5038 (or your chosen port).
2. Set `UseMockBackend` to `false` and `ApiBaseUrl` to the API URL.
3. Restart the frontend.
