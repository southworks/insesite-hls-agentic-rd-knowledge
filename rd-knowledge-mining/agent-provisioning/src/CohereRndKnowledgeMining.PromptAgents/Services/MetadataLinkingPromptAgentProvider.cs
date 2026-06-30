using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using CohereRndKnowledgeMining.Api.Host.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CohereRndKnowledgeMining.Api.Host.Services;

public sealed class MetadataLinkingPromptAgentProvider
{
    private readonly AzureFoundryOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<MetadataLinkingPromptAgentProvider> _logger;
    private readonly SemaphoreSlim _agentLoadLock = new(1, 1);
    private AIAgent? _agent;
    private string _resolvedAgentName = string.Empty;
    private string _resolvedInstructions = string.Empty;
    private string _resolvedOutputSchema = string.Empty;
    private MpcConfiguration _resolvedMcp = MpcConfiguration.Disabled();

    public MetadataLinkingPromptAgentProvider(
        IOptions<AzureFoundryOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<MetadataLinkingPromptAgentProvider> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<AIAgent> GetMetadataLinkingPromptAgentAsync(CancellationToken cancellationToken)
    {
        if (_agent is not null)
        {
            return _agent;
        }

        await _agentLoadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_agent is not null)
            {
                return _agent;
            }

            PromptAgentDefinition definition = LoadPromptAgentDefinition();

            if (string.IsNullOrWhiteSpace(_options.ProjectEndpoint))
            {
                throw new InvalidOperationException(
                    "Azure Foundry configuration is missing. Set AzureFoundry:ProjectEndpoint or AZURE_FOUNDRY_PROJECT_ENDPOINT.");
            }

            var credential = new DefaultAzureCredential();
            var projectEndpoint = new Uri(_options.ProjectEndpoint);
            var projectClient = new AIProjectClient(projectEndpoint, credential);
            var agentClient = new AgentAdministrationClient(projectEndpoint, credential);

            _resolvedAgentName = string.IsNullOrWhiteSpace(definition.FoundryAgentName)
                ? _options.MetadataLinkingPromptAgentName
                : definition.FoundryAgentName;

            _resolvedInstructions = LoadPromptInstructions(definition);
            _resolvedOutputSchema = LoadOutputSchema(definition);
            _resolvedMcp = LoadMcpConfiguration(definition);

            ProjectsAgentRecord agentRecord = (await agentClient
                    .GetAgentAsync(_resolvedAgentName, cancellationToken)
                    .ConfigureAwait(false))
                .Value;

            _agent = projectClient.AsAIAgent(agentRecord);

            _logger.LogInformation(
                "Resolved Foundry metadata-linking prompt agent {AgentName} (record {AgentId}).",
                _resolvedAgentName,
                agentRecord.Id);

            return _agent;
        }
        finally
        {
            _agentLoadLock.Release();
        }
    }

    public async Task<string> GetMetadataLinkingInstructionsAsync(CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            await GetMetadataLinkingPromptAgentAsync(cancellationToken).ConfigureAwait(false);
        }

        return _resolvedInstructions;
    }

    public async Task<MpcConfiguration> GetMcpConfigurationAsync(CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            await GetMetadataLinkingPromptAgentAsync(cancellationToken).ConfigureAwait(false);
        }

        return _resolvedMcp;
    }

    public async Task<string> GetOutputSchemaAsync(CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            await GetMetadataLinkingPromptAgentAsync(cancellationToken).ConfigureAwait(false);
        }

        return _resolvedOutputSchema;
    }

    private PromptAgentDefinition LoadPromptAgentDefinition()
    {
        string specPath = ResolvePath(_options.MetadataLinkingPromptAgentSpecPath);
        if (!File.Exists(specPath))
        {
            throw new FileNotFoundException(
                $"Prompt agent spec was not found at '{specPath}'.",
                specPath);
        }

        string json = File.ReadAllText(specPath);
        PromptAgentDefinition? definition = JsonSerializer.Deserialize<PromptAgentDefinition>(json);

        if (definition is null)
        {
            throw new InvalidOperationException(
                $"Prompt agent spec at '{specPath}' is invalid.");
        }

        string specDirectory = Path.GetDirectoryName(specPath) ?? string.Empty;
        return definition with { SpecDirectory = specDirectory };
    }

    private string LoadPromptInstructions(PromptAgentDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.InstructionsFile))
        {
            return string.Empty;
        }

        string instructionsPath = ResolveAgentPath(definition.SpecDirectory, definition.InstructionsFile);
        if (!File.Exists(instructionsPath))
        {
            throw new FileNotFoundException(
                $"Prompt agent instructions were not found at '{instructionsPath}'.",
                instructionsPath);
        }

        return File.ReadAllText(instructionsPath).Trim();
    }

    private MpcConfiguration LoadMcpConfiguration(PromptAgentDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.McpFile))
        {
            return MpcConfiguration.Disabled();
        }

        string mcpPath = ResolveAgentPath(definition.SpecDirectory, definition.McpFile);
        if (!File.Exists(mcpPath))
        {
            return MpcConfiguration.Disabled();
        }

        string mcpJson = File.ReadAllText(mcpPath);
        MpcConfiguration? mcp = JsonSerializer.Deserialize<MpcConfiguration>(mcpJson);
        if (mcp is null || !mcp.Enabled)
        {
            return MpcConfiguration.Disabled();
        }

        return new MpcConfiguration
        {
            Enabled = true,
            ServerName = mcp.ServerName,
            QueryTemplate = mcp.QueryTemplate,
            MaxItems = mcp.MaxItems <= 0 ? 3 : mcp.MaxItems
        };
    }

    private string LoadOutputSchema(PromptAgentDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.OutputSchemaFile))
        {
            return string.Empty;
        }

        string schemaPath = ResolveAgentPath(definition.SpecDirectory, definition.OutputSchemaFile);
        if (!File.Exists(schemaPath))
        {
            return string.Empty;
        }

        return File.ReadAllText(schemaPath).Trim();
    }

    private string ResolvePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }

        string fromContentRoot = Path.Combine(_hostEnvironment.ContentRootPath, relativePath);
        if (File.Exists(fromContentRoot))
        {
            return fromContentRoot;
        }

        return Path.Combine(AppContext.BaseDirectory, relativePath);
    }

    private string ResolveAgentPath(string specDirectory, string value)
    {
        if (Path.IsPathRooted(value))
        {
            return value;
        }

        if (!string.IsNullOrWhiteSpace(specDirectory))
        {
            string relativeToSpec = Path.Combine(specDirectory, value);
            if (File.Exists(relativeToSpec))
            {
                return relativeToSpec;
            }
        }

        return ResolvePath(value);
    }

    public sealed class MpcConfiguration
    {
        public bool Enabled { get; init; }

        public string ServerName { get; init; } = "metadata-mcp";

        public string QueryTemplate { get; init; } = "Find external context related to: {{input}}";

        public int MaxItems { get; init; } = 3;

        public static MpcConfiguration Disabled() => new();
    }

    private sealed record PromptAgentDefinition
    {
        public string Name { get; init; } = string.Empty;

        public string FoundryAgentName { get; init; } = string.Empty;

        public string InstructionsFile { get; init; } = string.Empty;

        public string McpFile { get; init; } = "mcp.json";

        public string OutputSchemaFile { get; init; } = "output-schema.json";

        public string Version { get; init; } = "1.0.0";

        public string SpecDirectory { get; init; } = string.Empty;
    }
}
