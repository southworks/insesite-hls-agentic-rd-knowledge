using Cohere.AgenticRDKnowledge.Shared.Contracts.Fabric;
using Cohere.AgenticRDKnowledge.WebApp.Models;
using Cohere.AgenticRDKnowledge.WebApp.Services;

namespace Cohere.AgenticRDKnowledge.WebApp.State;

public sealed class KnowledgePortfolioState(
    DatasetSeedCatalogService catalog,
    KnowledgeSessionStore sessionStore,
    IRdKnowledgeApiClient apiClient)
{
    public IReadOnlyList<SeedScenarioDefinition> IngestionScenarios { get; private set; } = [];
    public IReadOnlyList<SeedScenarioDefinition> QueryScenarios { get; private set; } = [];
    public IReadOnlyList<KnowledgeSessionSummary> ActiveSessions { get; private set; } = [];
    public FabricStoreSummary? FabricSummary { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }

    public event Action? OnChange;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        Error = null;
        Notify();

        try
        {
            IngestionScenarios = catalog.GetIngestionScenarios();
            QueryScenarios = catalog.GetQueryScenarios();
            ActiveSessions = sessionStore.GetSummaries();
            FabricSummary = await apiClient.GetFabricStoreSummaryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
            Notify();
        }
    }

    public void RefreshSessions()
    {
        ActiveSessions = sessionStore.GetSummaries();
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}
