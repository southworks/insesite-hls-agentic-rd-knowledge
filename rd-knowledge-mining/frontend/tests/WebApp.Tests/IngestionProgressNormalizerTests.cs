using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Agents;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Ingestion;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Studies;
using Cohere.AgenticRDKnowledge.WebApp.Models;
using Cohere.AgenticRDKnowledge.WebApp.Services;

namespace Cohere.AgenticRDKnowledge.WebApp.Tests;

public sealed class IngestionProgressNormalizerTests
{
    private const string KnownPostApprovalError =
        "Cannot output object of type String. Expecting one of [].";

    private static readonly HumanDecisionRecord ApprovedDecision =
        new(true, "Looks good.", DateTimeOffset.UtcNow);

    private static readonly HumanDecisionRecord DeniedDecision =
        new(false, "Not acceptable.", DateTimeOffset.UtcNow);

    [Fact]
    public void Normalize_approved_ING003_with_known_error_maps_to_completed_with_labeling_message()
    {
        var progress = CreateFailedProgress(KnownPostApprovalError);
        var scenario = CreateScenario("ingestion-ing-003", "approved_with_required_labeling");

        var normalized = IngestionProgressNormalizer.Normalize(progress, scenario, ApprovedDecision);

        Assert.Equal(WorkflowStatus.Completed, normalized.Status);
        Assert.Equal(IngestionStage.Completed, normalized.CurrentStage);
        Assert.Equal(ApprovedDecision, normalized.HumanDecision);
        Assert.Contains("required synthetic provenance labeling", normalized.StatusMessage);
        Assert.Null(normalized.FailureReason);
    }

    [Fact]
    public void Normalize_approved_ING001_with_known_error_maps_to_completed_with_persisted_message()
    {
        var progress = CreateFailedProgress(KnownPostApprovalError);
        var scenario = CreateScenario("ingestion-ing-001", "approved_persisted");

        var normalized = IngestionProgressNormalizer.Normalize(progress, scenario, ApprovedDecision);

        Assert.Equal(WorkflowStatus.Completed, normalized.Status);
        Assert.Equal("Ingestion approved. Knowledge embedded and saved to Vector DB.", normalized.StatusMessage);
    }

    [Fact]
    public void Normalize_approved_with_unrelated_error_stays_failed()
    {
        var progress = CreateFailedProgress("Something else went wrong.");
        var scenario = CreateScenario("ingestion-ing-003", "approved_with_required_labeling");

        var normalized = IngestionProgressNormalizer.Normalize(progress, scenario, ApprovedDecision);

        Assert.Equal(WorkflowStatus.Failed, normalized.Status);
        Assert.Equal("Something else went wrong.", normalized.FailureReason);
    }

    [Fact]
    public void Normalize_denied_with_known_error_stays_failed()
    {
        var progress = CreateFailedProgress(KnownPostApprovalError);
        var scenario = CreateScenario("ingestion-ing-004", "denied_not_persisted");

        var normalized = IngestionProgressNormalizer.Normalize(progress, scenario, DeniedDecision);

        Assert.Equal(WorkflowStatus.Failed, normalized.Status);
        Assert.Equal(IngestionStage.Failed, normalized.CurrentStage);
    }

    [Fact]
    public void IsKnownPostApprovalWorkflowOutputError_matches_framework_message()
    {
        Assert.True(IngestionProgressNormalizer.IsKnownPostApprovalWorkflowOutputError(KnownPostApprovalError));
        Assert.False(IngestionProgressNormalizer.IsKnownPostApprovalWorkflowOutputError("Other error"));
    }

    private static IngestionWorkflowProgress CreateFailedProgress(string failureReason) =>
        new(
            "exec-1",
            "case-02-approval-labeling",
            WorkflowStatus.Failed,
            IngestionStage.Failed,
            failureReason,
            null,
            null,
            null,
            null,
            null,
            [],
            failureReason);

    private static SeedScenarioDefinition CreateScenario(string scenarioId, string finalOutcome) =>
        new(
            scenarioId,
            "Ingestion",
            "Test scenario",
            "Description",
            "case-02-approval-labeling",
            "case-02-approval-labeling",
            null,
            "Approve",
            new StudySummary("case-02-approval-labeling", "Test", "—", "—", "—", ["ELN"]),
            FinalOutcome: finalOutcome);
}
