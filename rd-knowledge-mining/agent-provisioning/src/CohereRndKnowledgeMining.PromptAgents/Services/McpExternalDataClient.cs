namespace CohereRndKnowledgeMining.Api.Host.Services;

public interface IMcpExternalDataClient
{
    Task<IReadOnlyList<string>> QueryAsync(
        string serverName,
        string query,
        int maxItems,
        CancellationToken cancellationToken);
}

public sealed class StubMcpExternalDataClient : IMcpExternalDataClient
{
    public Task<IReadOnlyList<string>> QueryAsync(
        string serverName,
        string query,
        int maxItems,
        CancellationToken cancellationToken)
    {
        string[] candidates =
        [
            "External trial registry note: KRAS cohort linked with protocol KRAS-P2.",
            "External publication snippet: Drug-X is associated with improved ORR in KRAS-mutated NSCLC.",
            "External data catalog: biomarker panel BKM-KRAS linked to dataset LAB-KRAS-01."
        ];

        IReadOnlyList<string> items = candidates
            .Take(Math.Max(1, maxItems))
            .ToArray();

        return Task.FromResult(items);
    }
}
