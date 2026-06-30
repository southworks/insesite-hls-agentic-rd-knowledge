using Cohere.AgenticRDKnowledge.Shared.Contracts.Studies;
using Cohere.AgenticRDKnowledge.WebApp.Configuration;
using Cohere.AgenticRDKnowledge.WebApp.Models;
using Cohere.AgenticRDKnowledge.WebApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Cohere.AgenticRDKnowledge.WebApp.Tests;

public sealed class ScenarioCatalogTests
{
    private static PortfolioScenarioService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var options = Options.Create(
            configuration.GetSection(PortfolioScenariosOptions.SectionName).Get<PortfolioScenariosOptions>()
            ?? new PortfolioScenariosOptions());

        return new PortfolioScenarioService(options);
    }

    [Fact]
    public void Catalog_has_four_ingestion_and_two_query_scenarios()
    {
        var service = CreateService();

        Assert.Equal(4, service.GetIngestionScenarios().Count);
        Assert.Equal(2, service.GetQueryScenarios().Count);
        Assert.Equal(6, service.GetScenarios().Count);
    }

    [Fact]
    public void Each_ingestion_scenario_has_known_source_id()
    {
        var expected = new[]
        {
            "case-04-demo",
            "case-01-human-review",
            "case-02-approval-labeling",
            "case-03-sensitive-denied"
        };

        var sourceIds = CreateService()
            .GetIngestionScenarios()
            .Select(s => s.SourceId)
            .ToList();

        Assert.Equal(expected.OrderBy(x => x), sourceIds.OrderBy(x => x));
    }

    [Fact]
    public void Each_query_scenario_has_sample_question()
    {
        foreach (var scenario in CreateService().GetQueryScenarios())
        {
            Assert.False(string.IsNullOrWhiteSpace(scenario.SampleQuestion), scenario.ScenarioId);
        }
    }

    [Fact]
    public void ScenarioPickerFilter_returns_deny_scenario_for_deny_filter()
    {
        var scenarios = CreateService().GetIngestionScenarios();

        var filtered = ScenarioPickerFilter.Apply(scenarios, searchQuery: string.Empty, outcomeFilter: "Deny");

        var denied = Assert.Single(filtered);
        Assert.Equal("ING-004", denied.LegacyScenarioId);
        Assert.Equal("case-03-sensitive-denied", denied.SourceId);
    }

    [Fact]
    public void ResolveIngestionScenario_finds_by_source_id()
    {
        var service = CreateService();

        var scenario = service.ResolveIngestionScenario("case-04-demo");

        Assert.NotNull(scenario);
        Assert.Equal("ING-001", scenario!.LegacyScenarioId);
    }

    [Fact]
    public void Backend_payload_describes_ingestion_source_id()
    {
        var scenario = CreateService().GetScenario("ingestion-ing-001");
        Assert.NotNull(scenario);

        var payload = ScenarioBackendPayload.DescribeStartPayload(scenario!);

        Assert.Contains("case-04-demo", payload);
        Assert.Contains("ingestion/start", payload);
    }
}
