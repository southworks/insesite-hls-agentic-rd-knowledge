using CohereRndKnowledgeMining.Api.Host.Options;
using CohereRndKnowledgeMining.Api.Host.Services;
using CohereRndKnowledgeMining.Api.Host.Services.Integrations;
using CohereRndKnowledgeMining.Api.Host.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

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

// Foundry agents shared by both blocks.
builder.Services.AddSingleton<FoundryAgentProvider>();

// Stub integrations (TODO: replace with real Fabric + Vector DB wiring).
builder.Services.AddSingleton<IFabricRawSourceReader, StubFabricRawSourceReader>();
builder.Services.AddSingleton<IVectorKnowledgeWriter, StubVectorKnowledgeWriter>();
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
