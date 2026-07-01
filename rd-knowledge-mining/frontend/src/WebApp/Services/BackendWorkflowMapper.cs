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

    public static IngestionTranslationResult? ParseIngestionTranslation(string? raw) =>
        ParseWithSchemaFallback(
            raw,
            json => JsonSerializer.Deserialize<IngestionTranslationResult>(json, JsonOptions),
            result => !string.IsNullOrWhiteSpace(result?.Summary)
                && result.NormalizedFormats is not null
                && result.ConnectedPortals is not null,
            AgentOutputSchemaMapper.MapIngestionTranslation);

    public static MetadataLinkingResult? ParseMetadataLinking(string? raw) =>
        ParseWithSchemaFallback(
            raw,
            json => JsonSerializer.Deserialize<MetadataLinkingResult>(json, JsonOptions),
            result => !string.IsNullOrWhiteSpace(result?.Summary)
                && result.Entities is not null
                && result.Links is not null,
            AgentOutputSchemaMapper.MapMetadataLinking);

    public static SearchChatResult? ParseSearchChat(string? raw) =>
        ParseWithSchemaFallback(
            raw,
            json => JsonSerializer.Deserialize<SearchChatResult>(json, JsonOptions),
            result => !string.IsNullOrWhiteSpace(result?.Answer),
            AgentOutputSchemaMapper.MapSearchChat);

    public static CurationComplianceResult? ParseCurationCompliance(string? raw) =>
        ParseWithSchemaFallback(
            raw,
            json => JsonSerializer.Deserialize<CurationComplianceResult>(json, JsonOptions),
            result => !string.IsNullOrWhiteSpace(result?.Summary) && result.Flags is not null,
            AgentOutputSchemaMapper.MapCurationCompliance);

    private static T? ParseWithSchemaFallback<T>(
        string? raw,
        Func<string, T?> tryLegacy,
        Func<T?, bool> isUsable,
        Func<string, T?> trySchema) where T : class
    {
        var normalized = NormalizePayload(raw);
        if (normalized is null)
        {
            return null;
        }

        try
        {
            T? legacy = null;
            try
            {
                legacy = tryLegacy(normalized);
            }
            catch (JsonException)
            {
                // Legacy DTO shape mismatch; fall through to schema mapper.
            }

            if (isUsable(legacy))
            {
                return legacy;
            }

            return trySchema(normalized);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string? NormalizePayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();

        const string assistantPrefix = "[assistant]";
        var assistantIndex = trimmed.LastIndexOf(assistantPrefix, StringComparison.OrdinalIgnoreCase);
        if (assistantIndex >= 0)
        {
            trimmed = trimmed[(assistantIndex + assistantPrefix.Length)..].TrimStart();
        }

        trimmed = ExtractJsonFromMarkdownFence(trimmed);

        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            return null;
        }

        return trimmed;
    }

    private static string ExtractJsonFromMarkdownFence(string value)
    {
        const string jsonFence = "```json";
        var start = value.IndexOf(jsonFence, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            const string genericFence = "```";
            start = value.IndexOf(genericFence, StringComparison.Ordinal);
            if (start < 0)
            {
                return value;
            }

            start += genericFence.Length;
        }
        else
        {
            start += jsonFence.Length;
        }

        var end = value.IndexOf("```", start, StringComparison.Ordinal);
        return end < 0 ? value[start..].Trim() : value[start..end].Trim();
    }
}
