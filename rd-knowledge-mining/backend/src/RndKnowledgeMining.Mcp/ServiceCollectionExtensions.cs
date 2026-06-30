using System.Collections.Concurrent;
using System.Reflection;
using RndKnowledgeMining.Mcp.Adapters;
using RndKnowledgeMining.Mcp.Options;
using RndKnowledgeMining.Mcp.Startup;
using RndKnowledgeMining.Mcp.Tools;
using ModelContextProtocol.Server;

namespace RndKnowledgeMining.Mcp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRndKnowledgeMiningMcpServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PolicySeedOptions>(configuration.GetSection(PolicySeedOptions.SectionName));
        services.Configure<AzureSearchOptions>(configuration.GetSection(AzureSearchOptions.SectionName));
        services.Configure<AzureFoundryModelsOptions>(configuration.GetSection(AzureFoundryModelsOptions.SectionName));
        services.Configure<McpStartupOptions>(configuration.GetSection(McpStartupOptions.SectionName));
        services.Configure<FabricLakehouseOptions>(configuration.GetSection(FabricLakehouseOptions.SectionName));

        var searchOptions = configuration.GetSection(AzureSearchOptions.SectionName).Get<AzureSearchOptions>()
            ?? new AzureSearchOptions();
        var foundryOptions = configuration.GetSection(AzureFoundryModelsOptions.SectionName).Get<AzureFoundryModelsOptions>()
            ?? new AzureFoundryModelsOptions();
        var fabricLakehouseOptions = configuration.GetSection(FabricLakehouseOptions.SectionName).Get<FabricLakehouseOptions>()
            ?? new FabricLakehouseOptions();

        ValidateRequiredConfiguration(searchOptions, foundryOptions);
        ValidateFabricLakehouseConfiguration(fabricLakehouseOptions);

        services.AddSingleton(SearchClientFactory.CreateIndexClient(searchOptions));

        services.AddHttpClient<FoundryEmbeddingService>(client => client.Timeout = TimeSpan.FromMinutes(10))
            .AddFoundryResilience(foundryOptions);
        services.AddHttpClient<FoundryRerankService>(client => client.Timeout = TimeSpan.FromMinutes(10))
            .AddFoundryResilience(foundryOptions);

        services.AddSingleton<PolicyParser>();
        services.AddSingleton<SensitiveContentScanner>();
        services.AddSingleton<SearchIndexInitializer>();
        services.AddSingleton<KnowledgeIndexAdapter>();
        services.AddSingleton<PolicyIndexAdapter>();
        services.AddSingleton<PolicyIndexSeeder>();
        services.AddSingleton<PolicySeedRunner>();
        services.AddSingleton<IKnowledgeSearchService, AzureKnowledgeSearchService>();
        services.AddSingleton<IPolicySearchService, AzurePolicySearchService>();
        services.AddSingleton<KnowledgeSearchTools>();
        services.AddSingleton<CurationComplianceTools>();
        services.AddSingleton<FabricLakehouseClient>(sp =>
            FabricLakehouseClient.Create(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FabricLakehouseOptions>>().Value,
                sp.GetRequiredService<ILogger<FabricLakehouseClient>>()));
        services.AddSingleton<RawSourceService>();
        services.AddSingleton<RawSourceTools>();
        services.AddHostedService<McpStartupInitializer>();

        return services;
    }

    private static void ValidateRequiredConfiguration(
        AzureSearchOptions searchOptions,
        AzureFoundryModelsOptions foundryOptions)
    {
        if (string.IsNullOrWhiteSpace(searchOptions.Endpoint))
        {
            throw new InvalidOperationException("AzureSearch:Endpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(foundryOptions.EmbedEndpoint))
        {
            throw new InvalidOperationException("AzureFoundryModels:EmbedEndpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(foundryOptions.RerankEndpoint))
        {
            throw new InvalidOperationException("AzureFoundryModels:RerankEndpoint is required.");
        }
    }

    private static void ValidateFabricLakehouseConfiguration(FabricLakehouseOptions fabricLakehouseOptions)
    {
        if (string.IsNullOrWhiteSpace(fabricLakehouseOptions.WorkspaceName))
        {
            throw new InvalidOperationException("FabricLakehouse:WorkspaceName is required.");
        }

        if (string.IsNullOrWhiteSpace(fabricLakehouseOptions.LakehouseName))
        {
            throw new InvalidOperationException("FabricLakehouse:LakehouseName is required.");
        }
    }

    public static void PopulateToolDictionary(
        IServiceProvider serviceProvider,
        ConcurrentDictionary<string, McpServerTool[]> toolDictionary)
    {
        toolDictionary["knowledge-search"] = CreateTools(
            serviceProvider.GetRequiredService<KnowledgeSearchTools>());
        toolDictionary["curation-compliance"] = CreateTools(
            serviceProvider.GetRequiredService<CurationComplianceTools>());
        toolDictionary["raw-source"] = CreateTools(
            serviceProvider.GetRequiredService<RawSourceTools>());
    }

    private static McpServerTool[] CreateTools<T>(T target)
    {
        var tools = new List<McpServerTool>();
        var methods = typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null);

        foreach (var method in methods)
        {
            tools.Add(McpServerTool.Create(method, target));
        }

        return tools.ToArray();
    }
}
