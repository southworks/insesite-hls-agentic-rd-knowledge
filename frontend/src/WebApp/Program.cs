using Cohere.AgenticRDKnowledge.WebApp.Components;
using Cohere.AgenticRDKnowledge.WebApp.Configuration;
using Cohere.AgenticRDKnowledge.WebApp.Services;
using Cohere.AgenticRDKnowledge.WebApp.State;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<WorkflowPollingOptions>(configuration.GetSection(WorkflowPollingOptions.SectionName));
builder.Services.Configure<DatasetSeedOptions>(configuration.GetSection(DatasetSeedOptions.SectionName));

builder.Services.AddSingleton<DatasetSeedCatalogService>();
builder.Services.AddSingleton<MockWorkflowSimulator>();

var useMockBackend = configuration.GetValue("UseMockBackend", true);
if (useMockBackend)
{
    builder.Services.AddSingleton<IRdKnowledgeApiClient, MockRdKnowledgeApiClient>();
}
else
{
    builder.Services.AddHttpClient<IRdKnowledgeApiClient, RdKnowledgeApiClient>(client =>
    {
        client.BaseAddress = new Uri(configuration["ApiBaseUrl"] ?? "http://localhost:5038/");
    });
}

builder.Services.AddScoped<KnowledgeSessionStore>();
builder.Services.AddScoped<KnowledgePortfolioState>();
builder.Services.AddScoped<IngestionWorkspaceState>();
builder.Services.AddScoped<QueryWorkspaceState>();
builder.Services.AddScoped<WorkspaceSectionState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
