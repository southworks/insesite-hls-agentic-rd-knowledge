using CohereRndKnowledgeMining.Api.Host.Options;
using CohereRndKnowledgeMining.Api.Host.Services;
using CohereRndKnowledgeMining.Api.Host.Services.Integrations;
using CohereRndKnowledgeMining.Api.Host.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<AzureSearchOptions>(builder.Configuration.GetSection(AzureSearchOptions.SectionName));
builder.Services.Configure<AzureFoundryModelsOptions>(builder.Configuration.GetSection(AzureFoundryModelsOptions.SectionName));
builder.Services.Configure<McpIntegrationOptions>(builder.Configuration.GetSection(McpIntegrationOptions.SectionName));

builder.Services.Configure<AzureFoundryOptions>(options =>
{
    builder.Configuration.GetSection(AzureFoundryOptions.SectionName).Bind(options);

    string? endpoint = builder.Configuration["AZURE_FOUNDRY_PROJECT_ENDPOINT"]
        ?? Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_ENDPOINT");

    if (!string.IsNullOrWhiteSpace(endpoint))
    {
        options.ProjectEndpoint = endpoint;
    }
});

// Data source: Local or Fabric.
var dsOptions = builder.Configuration.GetSection(DataSourceOptions.SectionName).Get<DataSourceOptions>()
    ?? new DataSourceOptions();

if (dsOptions.Mode == DataSourceMode.Fabric)
{
    var fabOpts = dsOptions.FabricLakehouse ?? new FabricLakehouseOptions();
    if (string.IsNullOrWhiteSpace(fabOpts.WorkspaceName) || string.IsNullOrWhiteSpace(fabOpts.LakehouseName))
    {
        throw new InvalidOperationException(
            "DataSource:Mode is Fabric but FabricLakehouse:WorkspaceName and/or FabricLakehouse:LakehouseName are missing.");
    }

    builder.Services.AddSingleton(fabOpts);
    builder.Services.AddSingleton(sp =>
        FabricLakehouseClient.Create(fabOpts, sp.GetRequiredService<ILogger<FabricLakehouseClient>>()));
    builder.Services.AddSingleton<IFabricRawSourceReader, LocalRawSourceReader>();
    builder.Services.AddSingleton<IFabricRawSourceWriter, FabricRawSourceWriter>();
    builder.Services.AddSingleton(dsOptions);
    builder.Services.Configure<DatasetOptions>(builder.Configuration.GetSection(DatasetOptions.SectionName));
}
else
{
    var dsOpts = builder.Configuration.GetSection(DatasetOptions.SectionName).Get<DatasetOptions>()
        ?? new DatasetOptions();
    if (string.IsNullOrWhiteSpace(dsOpts.RootPath))
    {
        throw new InvalidOperationException(
            "DataSource:Mode is Local but Dataset:RootPath is missing.");
    }

    builder.Services.Configure<DatasetOptions>(builder.Configuration.GetSection(DatasetOptions.SectionName));
    builder.Services.AddSingleton<IFabricRawSourceReader, LocalRawSourceReader>();
    builder.Services.AddSingleton(dsOptions);
}

// Foundry agents shared by both blocks.
builder.Services.AddSingleton<FoundryAgentProvider>();

AzureSearchOptions vectorDbOptions = builder.Configuration.GetSection(AzureSearchOptions.SectionName).Get<AzureSearchOptions>()
    ?? new AzureSearchOptions();
AzureFoundryModelsOptions foundryModelOptions = builder.Configuration.GetSection(AzureFoundryModelsOptions.SectionName).Get<AzureFoundryModelsOptions>()
    ?? new AzureFoundryModelsOptions();

bool vectorWriteReady =
    !string.IsNullOrWhiteSpace(vectorDbOptions.Endpoint) &&
    !string.IsNullOrWhiteSpace(foundryModelOptions.EmbedEndpoint);

if (vectorWriteReady)
{
    builder.Services.AddHttpClient<FoundryEmbeddingClient>();
    builder.Services.AddSingleton<IVectorKnowledgeWriter, AzureVectorKnowledgeWriter>();
}
else
{
    // Keep a local fallback when Vector DB settings are not configured.
    builder.Services.AddSingleton<IVectorKnowledgeWriter, StubVectorKnowledgeWriter>();
}

McpIntegrationOptions mcpOptions = builder.Configuration.GetSection(McpIntegrationOptions.SectionName).Get<McpIntegrationOptions>()
    ?? new McpIntegrationOptions();

if (!string.IsNullOrWhiteSpace(mcpOptions.KnowledgeSearchEndpoint))
{
    builder.Services.AddHttpClient<McpKnowledgeIndexingClient>();
    builder.Services.AddSingleton<IMetadataLinkingIndexer, McpKnowledgeIndexingClient>();
}
else
{
    builder.Services.AddSingleton<IMetadataLinkingIndexer, FallbackMetadataLinkingIndexer>();
}

// Block 2 retrieval remains stubbed until MCP-backed retriever wiring is completed.
builder.Services.AddSingleton<IVectorKnowledgeRetriever, StubVectorKnowledgeRetriever>();

// Block 1 - Ingestion.
builder.Services.AddSingleton<IngestionWorkflowFactory>();
builder.Services.AddSingleton<InMemoryIngestionWorkflowStore>();
builder.Services.AddSingleton<IngestionWorkflowService>();

// Block 2 - Query (Search & Chat + Curate).
builder.Services.AddSingleton<QueryWorkflowFactory>();
builder.Services.AddSingleton<InMemoryQuerySessionStore>();
builder.Services.AddSingleton<QueryWorkflowService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
