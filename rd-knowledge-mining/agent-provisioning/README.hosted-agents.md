# Hosted Agents - R&D Knowledge Mining

Prompt-agent runtime assets for the metadata-linking flow.

This folder follows the same layout style as `agent-provisioning`:

- `agents/` declarative agent definitions
- `config/` runtime/provisioning configuration files
- `shared/` shared schemas and reusable contracts
- `src/` .NET source code

## Current agent

- `metadata-linking` (Foundry prompt agent name: `metadata-linking-agent`)

## Build

```powershell
dotnet build rd-knowledge-mining/hosted-agents/src/CohereRndKnowledgeMining.PromptAgents/CohereRndKnowledgeMining.PromptAgents.csproj
```

## Required configuration

Set `AZURE_FOUNDRY_PROJECT_ENDPOINT` (or bind `AzureFoundry:ProjectEndpoint`) before invoking the provider/service in a host.
