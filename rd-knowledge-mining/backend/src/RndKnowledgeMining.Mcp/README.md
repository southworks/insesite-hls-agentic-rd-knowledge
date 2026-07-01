# R&D Knowledge Mining MCP Server

MCP tool provider for Block 2 agents (`search-chat-agent`, `curation-compliance-agent`). Requires **Azure AI Search** and **Azure AI Foundry** (Cohere embed + rerank) — there is no local fallback mode.

## MCP Endpoints

| MCP endpoint | Tools |
| --- | --- |
| `/knowledge-search/mcp` | `search_rd_knowledge`, `get_knowledge_lineage`, `index_rd_knowledge` |
| `/curation-compliance/mcp` | `get_relevant_policies`, `get_policies_by_refs`, `flag_sensitive_content` |

Health check: `GET /health`

Default local URL: `http://localhost:5041`

## Data sources

| Index | Populated by |
| --- | --- |
| `rd-knowledge-evidence` | Block 1 ingestion workflow (Vector DB write after Knowledge Curator approval) |
| `rd-policy-knowledge` | Deploy-time or startup policy seed from `rd-knowledge-mining/policies/hls_policies.txt` |

Knowledge documents indexed by Block 1 should include `linkedEntities` and `lineageNarrative` fields so `get_knowledge_lineage` can resolve traceability from Azure AI Search.

## Required configuration

Set in `appsettings.json`, `appsettings.Deployment.local.json`, or environment variables:

- `AzureSearch:Endpoint`
- `AzureFoundryModels:EmbedEndpoint`
- `AzureFoundryModels:RerankEndpoint`

Leave `AzureFoundryModels:ApiKey` empty in Azure to use managed identity (`https://ai.azure.com/.default`).

## Local development

```powershell
cd rd-knowledge-mining/backend/src/RndKnowledgeMining.Mcp
# Configure appsettings.Deployment.local.json with your Azure endpoints
dotnet run
```

Deploy-style policy seeding without starting the web host:

```powershell
dotnet run -- --seed-policies
```
