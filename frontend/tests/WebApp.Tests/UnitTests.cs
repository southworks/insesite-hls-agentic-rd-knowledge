using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Ingestion;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Query;
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

    [Fact]
    public void BuildQueryTimeline_SeparatesChatAndCurateProcesses()
    {
        var session = new QuerySessionState(
            "query-test", "abc-2024", [new ChatMessage("user", "Q?", null, null, null, DateTimeOffset.UtcNow)],
            false, null, WorkflowStatus.Pending, QueryStage.ChatActive, "Active", null, null, []);
        var curation = new CurationWorkflowProgress(
            "cur-1", "query-test", WorkflowStatus.AwaitingHumanApproval,
            QueryStage.AwaitingComplianceReview, "Review", null, null, []);

        var steps = BackendWorkflowMapper.BuildQueryTimeline(session, curation);

        Assert.Equal(2, steps.Count);
        Assert.Equal("Process 1 — Search & Chat", steps[0].Label);
        Assert.Equal(WorkflowStepState.Completed, steps[0].State);
        Assert.Equal("Process 2 — Curate", steps[1].Label);
        Assert.Equal(WorkflowStepState.ActionRequired, steps[1].State);
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

    [Fact]
    public void ToQueryStageLabel_MapsComplianceReviewer()
    {
        Assert.Equal("Compliance Reviewer", WorkflowStageUi.ToQueryStageLabel(QueryStage.AwaitingComplianceReview));
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
    public void SubmitDecision_ApprovedIngestion_UpdatesVectorDbSummary()
    {
        var sim = new MockWorkflowSimulator();
        sim.StartIngestion("exec-1", "abc-2024");
        for (var i = 0; i < 5; i++) sim.AdvanceOnPoll("exec-1");
        sim.SubmitDecision("exec-1", true, "Looks good");

        var summary = sim.GetVectorDbSummary();
        Assert.Equal(1, summary.TotalStudies);
        Assert.Equal("abc-2024", summary.LastIngestedStudyId);
    }

    [Fact]
    public void Curation_OnlyStartsOnExplicitRequest()
    {
        var sim = new MockWorkflowSimulator();
        sim.BeginChatTurn("query-1", "Question?", "abc-2024");
        sim.AdvanceChatOnPoll("query-1");
        sim.AdvanceChatOnPoll("query-1");

        var session = sim.GetQuerySession("query-1")!;
        Assert.Null(session.CurationExecutionId);

        var curation = sim.StartCuration("query-1");
        Assert.NotNull(sim.GetExecution(curation.ExecutionId));
    }
}
