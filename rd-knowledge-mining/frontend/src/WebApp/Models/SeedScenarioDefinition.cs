using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Studies;

namespace Cohere.AgenticRDKnowledge.WebApp.Models;

public sealed record SeedScenarioDefinition(
    string ScenarioId,
    string Block,
    string Title,
    string Description,
    string StudyId,
    string SourceId,
    string? SampleQuestion,
    string OutcomeHint,
    StudySummary Study);

public sealed class ScenarioPickerFilter
{
    public static IReadOnlyList<SeedScenarioDefinition> Apply(
        IReadOnlyList<SeedScenarioDefinition> scenarios,
        string searchQuery,
        string? outcomeFilter)
    {
        var query = searchQuery.Trim();
        IEnumerable<SeedScenarioDefinition> filtered = scenarios;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(s =>
                s.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.StudyId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(outcomeFilter))
        {
            filtered = filtered.Where(s =>
                s.OutcomeHint.Equals(outcomeFilter, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToList();
    }
}
