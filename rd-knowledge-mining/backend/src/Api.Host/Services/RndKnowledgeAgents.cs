using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using CohereRndKnowledgeMining.Api.Host.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;

namespace CohereRndKnowledgeMining.Api.Host.Services;

/// <summary>
/// The four Foundry prompt agents used across the two blocks.
/// Block 1 (Ingestion) uses <see cref="IngestionTranslation"/> and <see cref="MetadataLinking"/>.
/// Block 2 (Query) uses <see cref="SearchChat"/> and <see cref="CurationCompliance"/>.
/// </summary>
public sealed class RndKnowledgeAgents
{
    public required AIAgent IngestionTranslation { get; init; }

    public required AIAgent MetadataLinking { get; init; }

    public required AIAgent SearchChat { get; init; }

    public required AIAgent CurationCompliance { get; init; }
}

public sealed class FoundryAgentProvider
{
    private readonly AzureFoundryOptions _options;
    private readonly ILogger<FoundryAgentProvider> _logger;
    private readonly SemaphoreSlim _agentLoadLock = new(1, 1);
    private RndKnowledgeAgents? _agents;

    public FoundryAgentProvider(IOptions<AzureFoundryOptions> options, ILogger<FoundryAgentProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RndKnowledgeAgents> GetAgentsAsync(CancellationToken cancellationToken)
    {
        if (_agents is not null)
        {
            return _agents;
        }

        await _agentLoadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_agents is not null)
            {
                return _agents;
            }

            if (string.IsNullOrWhiteSpace(_options.ProjectEndpoint))
            {
                throw new InvalidOperationException(
                    "Azure Foundry configuration is missing. Set AzureFoundry:ProjectEndpoint in configuration or the AZURE_FOUNDRY_PROJECT_ENDPOINT environment variable.");
            }

            var credential = new DefaultAzureCredential();
            var projectEndpoint = new Uri(_options.ProjectEndpoint);
            var projectClient = new AIProjectClient(projectEndpoint, credential);
            var agentClient = new AgentAdministrationClient(projectEndpoint, credential);

            _logger.LogInformation("Resolving Azure AI Foundry agents from project endpoint {Endpoint}", _options.ProjectEndpoint);

            AIAgent ingestionTranslation = await LoadPromptAgentAsync(
                    projectClient,
                    agentClient,
                    _options.IngestionTranslationAgentName,
                    cancellationToken)
                .ConfigureAwait(false);
            AIAgent metadataLinking = await LoadPromptAgentAsync(
                    projectClient,
                    agentClient,
                    _options.MetadataLinkingAgentName,
                    cancellationToken)
                .ConfigureAwait(false);
            AIAgent searchChat = await LoadPromptAgentAsync(
                    projectClient,
                    agentClient,
                    _options.SearchChatAgentName,
                    cancellationToken)
                .ConfigureAwait(false);
            AIAgent curationCompliance = await LoadPromptAgentAsync(
                    projectClient,
                    agentClient,
                    _options.CurationComplianceAgentName,
                    cancellationToken)
                .ConfigureAwait(false);

            _agents = new RndKnowledgeAgents
            {
                IngestionTranslation = ingestionTranslation,
                MetadataLinking = metadataLinking,
                SearchChat = searchChat,
                CurationCompliance = curationCompliance
            };

            return _agents;
        }
        finally
        {
            _agentLoadLock.Release();
        }
    }

    private async Task<AIAgent> LoadPromptAgentAsync(
        AIProjectClient projectClient,
        AgentAdministrationClient agentClient,
        string agentName,
        CancellationToken cancellationToken)
    {
        try
        {
            ProjectsAgentRecord agentRecord = (await agentClient
                    .GetAgentAsync(agentName, cancellationToken)
                    .ConfigureAwait(false))
                .Value;

            AIAgent agent = projectClient.AsAIAgent(agentRecord);

            _logger.LogInformation(
                "Resolved Foundry prompt agent {AgentName} (record {AgentId}) as {AgentType}.",
                agentName,
                agentRecord.Id,
                agent.GetType().Name);

            return agent;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Required Foundry prompt agent '{agentName}' could not be resolved. Verify the agent exists in the project and that the caller is authenticated.",
                ex);
        }
    }
}
