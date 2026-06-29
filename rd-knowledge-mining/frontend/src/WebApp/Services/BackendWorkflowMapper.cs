using System.Text.Json;
using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Agents;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Ingestion;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Query;

namespace Cohere.AgenticRDKnowledge.WebApp.Services;

public static class BackendWorkflowMapper
{
    public static IReadOnlyList<WorkflowTimelineStep> BuildIngestionTimeline(IngestionWorkflowProgress progress)
    {
        return
        [
            new WorkflowTimelineStep(
                "Ingestion & Translation",
                GetIngestionStepState(progress.CurrentStage, IngestionStage.IngestionTranslation)),
            new WorkflowTimelineStep(
                "Metadata & Linking",
                GetIngestionStepState(progress.CurrentStage, IngestionStage.MetadataLinking)),
            new WorkflowTimelineStep(
                "Knowledge Curator",
                GetIngestionStepState(progress.CurrentStage, IngestionStage.HumanApproval))
        ];
    }

    public static IReadOnlyList<WorkflowTimelineStep> BuildQueryTimeline(
        QuerySessionState? session,
        CurationWorkflowProgress? curation)
    {
        var chatState = GetChatStepState(session);
        var curateState = GetCurateStepState(session, curation);

        return
        [
            new WorkflowTimelineStep("Process 1 — Search & Chat", chatState),
            new WorkflowTimelineStep("Process 2 — Curate", curateState)
        ];
    }

    private static WorkflowStepState GetChatStepState(QuerySessionState? session)
    {
        if (session is null || session.Messages.Count == 0)
        {
            return WorkflowStepState.Pending;
        }

        if (session.IsChatRunning)
        {
            return WorkflowStepState.InProgress;
        }

        return WorkflowStepState.Completed;
    }

    private static WorkflowStepState GetCurateStepState(QuerySessionState? session, CurationWorkflowProgress? curation)
    {
        if (curation is null)
        {
            return session?.Messages.Count > 0 ? WorkflowStepState.Pending : WorkflowStepState.Pending;
        }

        return curation.CurrentStage switch
        {
            QueryStage.CurationRunning => WorkflowStepState.InProgress,
            QueryStage.AwaitingComplianceReview => WorkflowStepState.ActionRequired,
            QueryStage.Completed => WorkflowStepState.Completed,
            QueryStage.Failed => WorkflowStepState.Failed,
            _ => WorkflowStepState.Pending
        };
    }

    private static WorkflowStepState GetIngestionStepState(IngestionStage current, IngestionStage step)
    {
        if (current == IngestionStage.Failed)
        {
            return step <= current ? WorkflowStepState.Failed : WorkflowStepState.Pending;
        }

        if (current == IngestionStage.Completed)
        {
            return WorkflowStepState.Completed;
        }

        if (current > step)
        {
            return WorkflowStepState.Completed;
        }

        if (current == step)
        {
            return current == IngestionStage.HumanApproval && step == IngestionStage.HumanApproval
                ? WorkflowStepState.ActionRequired
                : WorkflowStepState.InProgress;
        }

        return WorkflowStepState.Pending;
    }
}

public enum WorkflowStepState
{
    Pending,
    InProgress,
    ActionRequired,
    Completed,
    Failed
}

public sealed record WorkflowTimelineStep(string Label, WorkflowStepState State);

public static class AgentOutputParser
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IngestionTranslationResult? ParseIngestionTranslation(string? json) =>
        Parse<IngestionTranslationResult>(json);

    public static MetadataLinkingResult? ParseMetadataLinking(string? json) =>
        Parse<MetadataLinkingResult>(json);

    public static SearchChatResult? ParseSearchChat(string? json) =>
        Parse<SearchChatResult>(json);

    public static CurationComplianceResult? ParseCurationCompliance(string? json) =>
        Parse<CurationComplianceResult>(json);

    private static T? Parse<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
