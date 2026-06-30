# API integration guide

The frontend calls **Api.Host** over HTTP from the Blazor Server process. Portfolio scenarios are loaded from `appsettings.json` (`PortfolioScenarios` section).

## Configuration

```json
{
  "ApiBaseUrl": "http://localhost:8080/",
  "PortfolioScenarios": { "Scenarios": [ ... ] },
  "WorkflowPolling": {
    "IntervalSeconds": 2,
    "MaxDurationMinutes": 10
  }
}
```

| Setting | Purpose |
|---------|---------|
| `ApiBaseUrl` | Base URL of `backend/src/Api.Host` (include trailing slash) |
| `PortfolioScenarios:Scenarios` | Ingestion and query scenario catalog for the portfolio page |
| `WorkflowPolling` | Ingestion and curation status polling intervals |

In Azure Container Apps, inject `ApiBaseUrl` with the API FQDN.

## Api.Host routes

| Operation | HTTP | Path |
|-----------|------|------|
| Health | GET | `/health` |
| Start ingestion | POST | `/api/rd-knowledge/ingestion/start` |
| Ingestion status | GET | `/api/rd-knowledge/ingestion/executions/{executionId}/status` |
| Ingestion HITL resume | POST | `/api/rd-knowledge/ingestion/sources/{sourceId}/executions/{executionId}/resume` |
| Ask (Search & Chat) | POST | `/api/rd-knowledge/query/ask` |
| Start curation | POST | `/api/rd-knowledge/query/curate/start` |
| Curation status | GET | `/api/rd-knowledge/query/curate/executions/{executionId}/status` |
| Curation HITL resume | POST | `/api/rd-knowledge/query/curate/sessions/{sessionId}/executions/{executionId}/resume` |

Backend DTOs are mirrored in `Contracts/Backend/BackendApiContracts.cs`. UI DTOs are mapped in `Services/BackendApiMapper.cs`.

## Local development

1. Start Api.Host:

```powershell
cd rd-knowledge-mining/backend/src/Api.Host
dotnet run --urls http://localhost:8080
```

2. Start the frontend:

```powershell
cd rd-knowledge-mining/frontend/src/WebApp
dotnet run
```

3. Open the frontend URL (typically `http://localhost:5147`).

Ensure `ApiBaseUrl` in `appsettings.Development.json` matches the API URL.

## Features not yet on the API

The frontend degrades gracefully when these endpoints are absent:

- **Vector DB summary** — shows empty counts
- **Study source documents** — sidebar shows empty state
- **Retrieval trace events** — omitted from workflow progress panels
