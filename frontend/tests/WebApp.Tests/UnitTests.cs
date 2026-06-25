using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Ingestion;
using Cohere.AgenticRDKnowledge.WebApp.Models;
using Cohere.AgenticRDKnowledge.WebApp.Services;

namespace Cohere.AgenticRDKnowledge.WebApp.Tests;

public class BackendWorkflowMapperTests
{
    [Fact]
    public void BuildIngestionTimeline_MarksCompletedStages()
    {
        var progress = new IngestionWorkflowProgress(
            "exec-1", "abc-2024", WorkflowStatus.AwaitingHumanApproval,
            IngestionStage.HumanApproval, "Review", null, null, null, null, null, []);

        var steps = BackendWorkflowMapper.BuildIngestionTimeline(progress);

        Assert.Equal(3, steps.Count);
        Assert.Equal(WorkflowStepState.Completed, steps[0].State);
        Assert.Equal(WorkflowStepState.Completed, steps[1].State);
        Assert.Equal(WorkflowStepState.ActionRequired, steps[2].State);
    }
}

public class ScenarioPickerFilterTests
{
    private static readonly SeedScenarioDefinition Sample = new(
        "s1", "Ingestion", "Study ABC", "Desc", "abc-2024", null, "Approve",
        new("abc-2024", "Title", "C", "P", "E", []));

    [Fact]
    public void Apply_FiltersBySearch()
    {
        var result = ScenarioPickerFilter.Apply([Sample], "ABC", null);
        Assert.Single(result);
    }

    [Fact]
    public void Apply_FiltersByOutcome()
    {
        var result = ScenarioPickerFilter.Apply([Sample], "", "Approve");
        Assert.Single(result);
    }
}

public class WorkflowStageUiTests
{
    [Fact]
    public void ToBusinessStatusLabel_MapsAwaitingApproval()
    {
        Assert.Equal("Action required", WorkflowStageUi.ToBusinessStatusLabel(WorkflowStatus.AwaitingHumanApproval));
    }
}

public class AgentOutputParserTests
{
    [Fact]
    public void ParseIngestionTranslation_DeserializesJson()
    {
        const string json = """{"summary":"ok","documentsProcessed":5,"duplicatesRemoved":1,"normalizedFormats":[],"connectedPortals":[]}""";
        var result = AgentOutputParser.ParseIngestionTranslation(json);
        Assert.NotNull(result);
        Assert.Equal(5, result.DocumentsProcessed);
    }
}

public class MockWorkflowSimulatorTests
{
    [Fact]
    public void AdvanceOnPoll_ReachesAwaitingHumanApproval()
    {
        var sim = new MockWorkflowSimulator();
        sim.StartIngestion("exec-1", "abc-2024");

        MockExecutionState state = sim.GetExecution("exec-1")!;
        for (var i = 0; i < 5; i++)
        {
            state = sim.AdvanceOnPoll("exec-1");
        }

        Assert.Equal(WorkflowStatus.AwaitingHumanApproval, state.Status);
    }

    [Fact]
    public void SubmitDecision_ApprovedIngestion_UpdatesFabricSummary()
    {
        var sim = new MockWorkflowSimulator();
        sim.StartIngestion("exec-1", "abc-2024");
        for (var i = 0; i < 5; i++) sim.AdvanceOnPoll("exec-1");
        sim.SubmitDecision("exec-1", true, "Looks good");

        var fabric = sim.GetFabricSummary();
        Assert.Equal(1, fabric.TotalStudies);
        Assert.Equal("abc-2024", fabric.LastIngestedStudyId);
    }
}
