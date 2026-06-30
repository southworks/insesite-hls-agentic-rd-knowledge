using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.WebApp.Configuration;
using Cohere.AgenticRDKnowledge.WebApp.Models;
using Microsoft.Extensions.Options;

namespace Cohere.AgenticRDKnowledge.WebApp.Services;

public sealed class PortfolioScenarioService
{
    private readonly IReadOnlyList<SeedScenarioDefinition> _scenarios;

    public PortfolioScenarioService(IOptions<PortfolioScenariosOptions> options)
    {
        _scenarios = options.Value.Scenarios ?? [];
    }

    public IReadOnlyList<SeedScenarioDefinition> GetScenarios() => _scenarios;

    public IReadOnlyList<SeedScenarioDefinition> GetIngestionScenarios() =>
        _scenarios.Where(s => s.Block.Equals("Ingestion", StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<SeedScenarioDefinition> GetQueryScenarios() =>
        _scenarios.Where(s => s.Block.Equals("Query", StringComparison.OrdinalIgnoreCase)).ToList();

    public SeedScenarioDefinition? GetScenario(string scenarioId) =>
        _scenarios.FirstOrDefault(s => s.ScenarioId.Equals(scenarioId, StringComparison.OrdinalIgnoreCase));

    public SeedScenarioDefinition? GetScenarioByStudyId(string studyId, WorkflowBlock block) =>
        _scenarios.FirstOrDefault(s =>
            s.StudyId.Equals(studyId, StringComparison.OrdinalIgnoreCase) &&
            s.Block.Equals(block.ToString(), StringComparison.OrdinalIgnoreCase));

    public string? GetSourceIdByStudyId(string studyId, WorkflowBlock block) =>
        GetScenarioByStudyId(studyId, block)?.SourceId is { Length: > 0 } sourceId
            ? sourceId
            : null;
}
