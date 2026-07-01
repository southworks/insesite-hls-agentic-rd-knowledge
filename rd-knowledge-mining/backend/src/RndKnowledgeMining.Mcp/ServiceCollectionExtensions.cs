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
        services.Configure<DataSourceOptions>(configuration.GetSection(DataSourceOptions.SectionName));
        services.Configure<DatasetOptions>(configuration.GetSection(DatasetOptions.SectionName));

        var searchOptions = configuration.GetSection(AzureSearchOptions.SectionName).Get<AzureSearchOptions>()
            ?? new AzureSearchOptions();
        var foundryOptions = configuration.GetSection(AzureFoundryModelsOptions.SectionName).Get<AzureFoundryModelsOptions>()
            ?? new AzureFoundryModelsOptions();
        var dataSourceOptions = configuration.GetSection(DataSourceOptions.SectionName).Get<DataSourceOptions>()
            ?? new DataSourceOptions();
        var datasetOptions = configuration.GetSection(DatasetOptions.SectionName).Get<DatasetOptions>()
            ?? new DatasetOptions();

        var fabricLakehouseOptions = dataSourceOptions.FabricLakehouse ?? new FabricLakehouseOptions();

        ValidateRequiredConfiguration(searchOptions, foundryOptions);

        if (dataSourceOptions.Mode == DataSourceMode.Fabric)
        {
            ValidateFabricLakehouseConfiguration(fabricLakehouseOptions);
            services.Configure<FabricLakehouseOptions>(
                configuration.GetSection($"{DataSourceOptions.SectionName}:{FabricLakehouseOptions.SectionName}"));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(datasetOptions.RootPath))
            {
                throw new InvalidOperationException(
                    "DataSource:Mode is Local but Dataset:RootPath is missing.");
            }
        }

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
        services.AddNormalizedDocumentStore(configuration);

        if (dataSourceOptions.Mode == DataSourceMode.Fabric)
        {
            services.AddSingleton<FabricLakehouseClient>(_ =>
                FabricLakehouseClient.Create(
                    fabricLakehouseOptions,
                    _.GetRequiredService<ILogger<FabricLakehouseClient>>()));
            services.AddSingleton<IRawSourceService, FabricRawSourceService>();
        }
        else
        {
            services.AddSingleton<IRawSourceService>(sp =>
                new LocalRawSourceService(
                    datasetOptions.RootPath,
                    sp.GetRequiredService<ILogger<LocalRawSourceService>>()));
        }

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
