using Cohere.AgenticRDKnowledge.Shared.Contracts.VectorDb;
using Cohere.AgenticRDKnowledge.WebApp.Models;
using Cohere.AgenticRDKnowledge.WebApp.Services;

namespace Cohere.AgenticRDKnowledge.WebApp.State;

public sealed class KnowledgePortfolioState
{
    private readonly IRdKnowledgeApiClient _apiClient;
    private readonly KnowledgeSessionStore _sessionStore;

    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }
    public VectorDbStoreSummary? VectorDbSummary { get; private set; }
    public IReadOnlyList<SeedScenarioDefinition> IngestionScenarios { get; private set; } = [];
    public IReadOnlyList<SeedScenarioDefinition> QueryScenarios { get; private set; } = [];
    public IReadOnlyList<KnowledgeSessionSummary> ActiveSessions { get; private set; } = [];

    public event Action? OnChange;

    public KnowledgePortfolioState(
        IRdKnowledgeApiClient apiClient,
        KnowledgeSessionStore sessionStore,
        DatasetSeedCatalogService catalog)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        IngestionScenarios = catalog.GetIngestionScenarios();
        QueryScenarios = catalog.GetQueryScenarios();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        Error = null;
        Notify();

        try
        {
            VectorDbSummary = await _apiClient.GetVectorDbStoreSummaryAsync(cancellationToken);
            RefreshSessions();
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
        ActiveSessions = _sessionStore.GetSummaries();
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}
