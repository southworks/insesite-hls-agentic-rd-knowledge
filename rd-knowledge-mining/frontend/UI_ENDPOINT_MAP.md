# UI Endpoint Map

This document maps **UI controls** on each page to the REST endpoints they trigger. Route constants live in [`src/WebApp/Contracts/Backend/RdKnowledgeBackendContracts.cs`](src/WebApp/Contracts/Backend/RdKnowledgeBackendContracts.cs); HTTP calls are implemented in [`src/WebApp/Services/RdKnowledgeApiClient.cs`](src/WebApp/Services/RdKnowledgeApiClient.cs).

For integration notes and the scenario catalog, see [`MOCK_BACKEND.md`](MOCK_BACKEND.md).

## How calls reach the backend

All workflow operations go through `IRdKnowledgeApiClient` (`RdKnowledgeApiClient`).

Polling loops in [`QueryWorkspaceState`](src/WebApp/State/QueryWorkspaceState.cs) and [`IngestionWorkspaceState`](src/WebApp/State/IngestionWorkspaceState.cs) repeat status GETs every `WorkflowPolling:IntervalSeconds` (default **2 seconds**) until the workflow reaches a terminal or HITL state.

Base URL is configured as `ApiBaseUrl` in `appsettings.json` (e.g. `http://localhost:8080/`).

## Scenario catalog source

Portfolio scenarios are loaded from **`PortfolioScenarios:Scenarios`** in [`appsettings.json`](src/WebApp/appsettings.json) via [`PortfolioScenarioService`](src/WebApp/Services/PortfolioScenarioService.cs). Six canonical cases (ING-001–004, QRY-001–002) mirror `dataset-seed/cases/`.

### What each scenario sends to Api.Host

| Block | User action | API | Body |
|-------|-------------|-----|------|
| Ingestion | Start ingestion workflow | `POST /api/rd-knowledge/ingestion/start` | `{ "sourceId": "<case-folder>" }` |
| Ingestion | Approve / Deny (curator) | `POST .../sources/{sourceId}/executions/{executionId}/resume` | `{ "approved", "reviewerComment" }` |
| Query | Open from portfolio (auto) or Send | `POST /api/rd-knowledge/query/ask` | `{ "sessionId", "question" }` |
| Query | Curate | `POST /api/rd-knowledge/query/curate/start` | `{ "sessionId" }` |
| Query | Approve / Deny (compliance) | `POST .../sessions/{sessionId}/executions/{executionId}/resume` | `{ "approved", "reviewerComment" }` |

**Not sent:** scenario title, description, study card fields, `legacyScenarioId`, `outcomeHint`, or query `studyScope` (frontend cache only).

Active session list is in-memory per browser circuit (`KnowledgeSessionStore`).

---

## Portfolio — [`KnowledgePortfolio.razor`](src/WebApp/Components/Pages/KnowledgePortfolio.razor)

| UI control | Handler | Client method | HTTP | Notes |
|------------|---------|---------------|------|-------|
| *(page load)* | `KnowledgePortfolioState.LoadAsync` | `GetVectorDbStoreSummaryAsync` | *(stub — empty summary)* | Vector DB card |
| **Run ingestion** | `RunIngestionAsync` | — | — | Opens session, navigates to `/ingestion/{studyId}` |
| **Run query** | `RunQueryAsync` | — | — | Opens session, navigates to `/query/{sessionId}`; ask fires on workspace load |
| **Open** (active sessions) | `OpenSessionAsync` | — | — | Navigation only |

---

## Ingestion — [`IngestionWorkspace.razor`](src/WebApp/Components/Pages/IngestionWorkspace.razor)

| UI control | Handler | Client method | HTTP | Notes |
|------------|---------|---------------|------|-------|
| *(page load)* | `IngestionWorkspaceState.LoadAsync` | `GetStudyDocumentsAsync` | *(stub — empty list)* | Sidebar |
| **Start ingestion workflow** | `StartWorkflowAsync` | `StartIngestionWorkflowAsync` | `POST /api/rd-knowledge/ingestion/start` | Body: `{ sourceId }` from scenario |
| *(polling)* | `PollLoopAsync` | `GetIngestionStatusAsync` | `GET /api/rd-knowledge/ingestion/executions/{executionId}/status` | Every 2s |
| **Approve** / **Deny** | `SubmitDecisionAsync` | `SubmitIngestionDecisionAsync` | `POST /api/rd-knowledge/ingestion/sources/{sourceId}/executions/{executionId}/resume` | |

---

## Query — [`QueryWorkspace.razor`](src/WebApp/Components/Pages/QueryWorkspace.razor)

| UI control | Handler | Client method | HTTP | Notes |
|------------|---------|---------------|------|-------|
| *(page load)* | `LoadAsync` | `GetQuerySessionAsync` then `SendChatMessageAsync` | `POST /api/rd-knowledge/query/ask` | Auto-sends `sampleQuestion` when transcript empty |
| **Send** | `SendMessageAsync` | `SendChatMessageAsync` | `POST /api/rd-knowledge/query/ask` | |
| **Curate** | `StartCurationAsync` | `StartCurationAsync` | `POST /api/rd-knowledge/query/curate/start` | |
| *(curation polling)* | `CurationPollLoopAsync` | `GetCurationStatusAsync` | `GET /api/rd-knowledge/query/curate/executions/{executionId}/status` | |
| **Approve** / **Deny** | `SubmitDecisionAsync` | `SubmitCurationDecisionAsync` | `POST /api/rd-knowledge/query/curate/sessions/{sessionId}/executions/{executionId}/resume` | |
