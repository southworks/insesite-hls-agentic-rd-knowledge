# UI Endpoint Map

This document maps **UI controls** on each page to the **hypothetical REST endpoints** they trigger when `UseMockBackend` is `false`. Route constants live in [`src/WebApp/Contracts/Backend/RdKnowledgeBackendContracts.cs`](src/WebApp/Contracts/Backend/RdKnowledgeBackendContracts.cs); HTTP calls are implemented in [`src/WebApp/Services/RdKnowledgeApiClient.cs`](src/WebApp/Services/RdKnowledgeApiClient.cs).

For mock mode behavior and backend replacement steps, see [`MOCK_BACKEND.md`](MOCK_BACKEND.md).

## How calls reach the backend

All workflow operations go through `IRdKnowledgeApiClient`. When `UseMockBackend` is `true` (default), the same interface methods run in memory via `MockRdKnowledgeApiClient` — no HTTP is sent.

Polling loops in [`QueryWorkspaceState`](src/WebApp/State/QueryWorkspaceState.cs) and [`IngestionWorkspaceState`](src/WebApp/State/IngestionWorkspaceState.cs) repeat status/session GETs every `WorkflowPolling:IntervalSeconds` (default **2 seconds**) until the workflow reaches a terminal or HITL state.

Base URL is configured as `ApiBaseUrl` in `appsettings.json` (e.g. `http://localhost:5038/`).

---

## Portfolio — [`KnowledgePortfolio.razor`](src/WebApp/Components/Pages/KnowledgePortfolio.razor)

| UI control | Handler | Client method | HTTP | Notes |
|------------|---------|---------------|------|-------|
| *(page load)* | `KnowledgePortfolioState.LoadAsync` | `GetVectorDbStoreSummaryAsync` | `GET /api/rd-knowledge/vector-db/summary` | Loads Vector DB summary card |
| **Retry** (error state) | `LoadAsync` | `GetVectorDbStoreSummaryAsync` | `GET /api/rd-knowledge/vector-db/summary` | Same as page load |
| **Run ingestion** | `RunIngestionAsync` | — | — | No API. Opens session in `KnowledgeSessionStore`, navigates to `/ingestion/{studyId}` |
| **Run query** | `RunQueryAsync` | `StartQueryWorkflowAsync` | `POST /api/rd-knowledge/query/sessions/{sessionId}/workflow/start` | Then navigates to `/query/{sessionId}/{executionId}` |
| **Open** (active sessions) | `OpenSessionAsync` | — | — | Navigation only; API calls happen on workspace page load |

**Not from API:** Ingestion and query scenario lists come from local `dataset-seed` via `DatasetSeedCatalogService`. Active session list is in-memory per browser circuit (`KnowledgeSessionStore`).

---

## Ingestion — [`IngestionWorkspace.razor`](src/WebApp/Components/Pages/IngestionWorkspace.razor)

| UI control | Handler | Client method | HTTP | Notes |
|------------|---------|---------------|------|-------|
| *(page load)* | `IngestionWorkspaceState.LoadAsync` | `GetStudyDocumentsAsync` | `GET /api/rd-knowledge/studies/{studyId}/documents` | Sidebar source material |
| *(page load, with `{ExecutionId}`)* | `LoadAsync` | `GetIngestionStatusAsync` | `GET /api/rd-knowledge/executions/{executionId}/ingestion/status` | Restores workflow progress; starts polling if running |
| *(polling while running)* | `PollLoopAsync` | `GetIngestionStatusAsync` | `GET /api/rd-knowledge/executions/{executionId}/ingestion/status` | Every 2s until terminal or HITL |
| **Start ingestion workflow** | `StartWorkflowAsync` | `StartIngestionWorkflowAsync` | `POST /api/rd-knowledge/studies/{studyId}/ingestion/workflow/start` | Then status GET + polling |
| **Start ingestion workflow** (cont.) | `StartWorkflowAsync` | `GetIngestionStatusAsync` | `GET /api/rd-knowledge/executions/{executionId}/ingestion/status` | Immediate fetch after start |
| **Approve** / **Deny** (Knowledge Curator) | `SubmitDecisionAsync` | `SubmitIngestionDecisionAsync` | `POST /api/rd-knowledge/executions/{executionId}/ingestion/resume` | Body: `{ approved, notes }` |
| **Approve** / **Deny** (cont.) | `SubmitDecisionAsync` | `GetIngestionStatusAsync` | `GET /api/rd-knowledge/executions/{executionId}/ingestion/status` | Refreshes progress after decision |
| **Retry** (error state) | `ReloadAsync` | *(same as page load)* | *(see above)* | |
| **← Portfolio** | `GoHome` | — | — | Navigation only |

---

## Query — [`QueryWorkspace.razor`](src/WebApp/Components/Pages/QueryWorkspace.razor)

| UI control | Handler | Client method | HTTP | Notes |
|------------|---------|---------------|------|-------|
| *(page load, with `{ExecutionId}`)* | `QueryWorkspaceState.LoadAsync` | `GetQuerySessionAsync` | `GET /api/rd-knowledge/executions/{executionId}/query/session` | Chat transcript and session state |
| *(page load, curation started)* | `LoadAsync` | `GetCurationStatusAsync` | `GET /api/rd-knowledge/executions/{executionId}/query/status` | Curation & Compliance progress |
| *(polling, chat running)* | `ChatPollLoopAsync` | `GetQuerySessionAsync` | `GET /api/rd-knowledge/executions/{executionId}/query/session` | Every 2s until chat completes |
| *(polling, curation running)* | `CurationPollLoopAsync` | `GetCurationStatusAsync` | `GET /api/rd-knowledge/executions/{executionId}/query/status` | Every 2s until terminal or HITL |
| *(polling, curation cont.)* | `CurationPollLoopAsync` | `GetQuerySessionAsync` | `GET /api/rd-knowledge/executions/{executionId}/query/session` | Keeps session in sync |
| **Send** | `SendMessageAsync` | `SendChatMessageAsync` | `POST /api/rd-knowledge/executions/{executionId}/query/chat` | Body: `{ question, studyScope }` |
| **Send** (cont.) | `SendMessageAsync` | *(poll)* `GetQuerySessionAsync` | `GET .../query/session` | Polls until assistant response completes |
| **Curate** | `StartCurationAsync` | `StartCurationAsync` | `POST /api/rd-knowledge/executions/{executionId}/query/curate` | |
| **Curate** (cont.) | `StartCurationAsync` | `GetCurationStatusAsync` | `GET .../query/status` | |
| **Curate** (cont.) | `StartCurationAsync` | `GetQuerySessionAsync` | `GET .../query/session` | Then curation polling |
| **Approve** / **Deny** (Compliance Reviewer) | `SubmitDecisionAsync` | `SubmitCurationDecisionAsync` | `POST /api/rd-knowledge/executions/{executionId}/query/resume` | Body: `{ approved, notes }` |
| **Approve** / **Deny** (cont.) | `SubmitDecisionAsync` | `GetCurationStatusAsync` | `GET .../query/status` | |
| **Approve** / **Deny** (cont.) | `SubmitDecisionAsync` | `GetQuerySessionAsync` | `GET .../query/session` | |
| **Retry** (error state) | `ReloadAsync` | *(same as page load)* | *(see above)* | |
| **← Portfolio** | `GoHome` | — | — | Navigation only |

---

## Full route reference

| Method | Path |
|--------|------|
| `GET` | `/health` |
| `GET` | `/api/rd-knowledge/vector-db/summary` |
| `GET` | `/api/rd-knowledge/studies/{studyId}/documents` |
| `POST` | `/api/rd-knowledge/studies/{studyId}/ingestion/workflow/start` |
| `GET` | `/api/rd-knowledge/executions/{executionId}/ingestion/status` |
| `POST` | `/api/rd-knowledge/executions/{executionId}/ingestion/resume` |
| `POST` | `/api/rd-knowledge/query/sessions/{sessionId}/workflow/start` |
| `GET` | `/api/rd-knowledge/executions/{executionId}/query/session` |
| `POST` | `/api/rd-knowledge/executions/{executionId}/query/chat` |
| `POST` | `/api/rd-knowledge/executions/{executionId}/query/curate` |
| `GET` | `/api/rd-knowledge/executions/{executionId}/query/status` |
| `POST` | `/api/rd-knowledge/executions/{executionId}/query/resume` |
