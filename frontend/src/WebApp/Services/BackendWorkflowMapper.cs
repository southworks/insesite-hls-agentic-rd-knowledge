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
                "Human approval",
                GetIngestionStepState(progress.CurrentStage, IngestionStage.HumanApproval))
        ];
    }

    public static IReadOnlyList<WorkflowTimelineStep> BuildQueryTimeline(QueryWorkflowProgress progress)
    {
        return
        [
            new WorkflowTimelineStep(
                "Search & Chat",
                GetQueryStepState(progress.CurrentStage, QueryStage.SearchChat)),
            new WorkflowTimelineStep(
                "Curation & Compliance",
                GetQueryStepState(progress.CurrentStage, QueryStage.CurationCompliance)),
            new WorkflowTimelineStep(
                "Human approval",
                GetQueryStepState(progress.CurrentStage, QueryStage.HumanApproval))
        ];
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

    private static WorkflowStepState GetQueryStepState(QueryStage current, QueryStage step)
    {
        if (current == QueryStage.Failed)
        {
            return step <= current ? WorkflowStepState.Failed : WorkflowStepState.Pending;
        }

        if (current == QueryStage.Completed)
        {
            return WorkflowStepState.Completed;
        }

        if (current > step)
        {
            return WorkflowStepState.Completed;
        }

        if (current == step)
        {
            return current == QueryStage.HumanApproval && step == QueryStage.HumanApproval
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
