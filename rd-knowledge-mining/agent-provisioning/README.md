# Agent Provisioning — R&D Knowledge Mining

Agent-as-code definitions for the R&D Knowledge Mining demo. Pattern mirrors `loan-and-mortage/agent-provisioning`.

## Agents

| Agent | Block | Responsibility | MCP path |
| --- | --- | --- | --- |
| `search-chat-agent` | 2 (Search & Chat) | Grounded Q&A over the Vector DB with citations and lineage | `/knowledge-search/mcp` |

Additional agents (`ingestion-translation-agent`, `metadata-linking-agent`, `curation-compliance-agent`) will be added in follow-up steps.

All agents use the Foundry model deployment **Cohere Command A** (`cohere-command-a`).

## Agent-as-code layout

```text
agents/
  search-chat-agent/
    agent.json
    instructions.md
    mcp.json
    governance.yaml
    rogue.yaml
shared/
  agent-structured-output.schema.json
  search-chat-structured-output.schema.json
config/
  provisioning.json
```

## Search & Chat structured output

`search-chat-agent` returns strict JSON with:

- `summary` — user-facing grounded answer
- `decision` — `Answered` | `Insufficient Evidence` | `Clarification Needed`
- `evidence` — rationale tied to retrieved passages
- `citations` — array of citation strings
- `lineage` — optional document/dataset/study lineage narrative

The backend `QueryWorkflowService` extracts `summary` as the chat answer and merges `citations` with retriever output.

## Configuration

`config/provisioning.json`:

```json
{
  "ProjectEndpoint": "",
  "ModelDeploymentName": "cohere-command-a",
  "McpBaseUrl": ""
}
```

Environment overrides:

- `AZURE_FOUNDRY_PROJECT_ENDPOINT` or `FOUNDRY_PROJECT_ENDPOINT`
- `AZURE_AI_MODEL_DEPLOYMENT_NAME` or `ModelDeploymentName`
- `MCP_BASE_URL`

## Provision locally

Until the R&D provisioning CLI is added, reuse the loan provisioning project pointed at this agents folder:

```powershell
dotnet run --project loan-and-mortage/agent-provisioning/src/CohereLoanAndMortgage.AgentProvisioning -- `
  --config rd-knowledge-mining/agent-provisioning/config/provisioning.local.json `
  --agents rd-knowledge-mining/agent-provisioning/agents
```

Create `config/provisioning.local.json` with your Foundry endpoint and MCP base URL.

> **Note:** The loan provisioning CLI appends generic structured-output instructions. `search-chat-agent` also defines citation/lineage rules in `instructions.md` and uses `search-chat-structured-output.schema.json`.

## MCP tools (expected)

The `knowledge-search` MCP server is not implemented yet. When added, it should expose:

| Tool | Purpose |
| --- | --- |
| `search_rd_knowledge` | Vector search + Cohere rerank over the R&D knowledge index (`sessionId`, `query`, `topK`) |
| `get_knowledge_lineage` | Resolve document/dataset/study links for a passage (`sessionId`, `passageId`) |

Until the MCP host exists, the API stub retriever injects passages into the prompt and the agent can answer from that context alone.

## Governance

`governance.yaml` denies ingestion and curation tools so Search & Chat stays within the query boundary. `rogue.yaml` flags repeated `curate_session_responses` calls as cross-stage escalation.
