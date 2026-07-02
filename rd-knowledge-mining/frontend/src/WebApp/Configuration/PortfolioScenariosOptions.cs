using Cohere.AgenticRDKnowledge.WebApp.Models;

namespace Cohere.AgenticRDKnowledge.WebApp.Configuration;

public sealed class PortfolioScenariosOptions
{
    public const string SectionName = "PortfolioScenarios";

    public List<SeedScenarioDefinition> Scenarios { get; set; } = [];
}
