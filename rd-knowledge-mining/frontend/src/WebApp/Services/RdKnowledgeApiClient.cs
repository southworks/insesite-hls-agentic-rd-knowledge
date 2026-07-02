using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Backend;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Ingestion;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Query;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Studies;
using Cohere.AgenticRDKnowledge.Shared.Contracts.VectorDb;

namespace Cohere.AgenticRDKnowledge.WebApp.Services;

public interface IRdKnowledgeApiClient
{
    Task<VectorDbStoreSummary> GetVectorDbStoreSummaryAsync(CancellationToken cancellationToken = default);
    Task<StudyDocumentsResponse> GetStudyDocumentsAsync(string studyId, CancellationToken cancellationToken = default);
    Task<StartIngestionWorkflowResponse> StartIngestionWorkflowAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<IngestionWorkflowProgress> GetIngestionStatusAsync(string executionId, CancellationToken cancellationToken = default);
    Task<SubmitIngestionDecisionResponse> SubmitIngestionDecisionAsync(string executionId, string sourceId, SubmitIngestionDecisionRequest request, CancellationToken cancellationToken = default);
    Task<QuerySessionState> GetQuerySessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<QuerySessionState> SendChatMessageAsync(string sessionId, SendChatMessageRequest request, CancellationToken cancellationToken = default);
    Task<StartCurationResponse> StartCurationAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<CurationWorkflowProgress> GetCurationStatusAsync(string curationExecutionId, CancellationToken cancellationToken = default);
    Task<SubmitQueryDecisionResponse> SubmitCurationDecisionAsync(string sessionId, string curationExecutionId, SubmitQueryDecisionRequest request, CancellationToken cancellationToken = default);
    Task<bool> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed class RdKnowledgeApiClient(
    HttpClient httpClient,
    QuerySessionCache querySessionCache,
    PortfolioScenarioService scenarios) : IRdKnowledgeApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<VectorDbStoreSummary> GetVectorDbStoreSummaryAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<VectorDbStoreSummaryResponse>(
            RdKnowledgeBackendRoutes.GetVectorDbSummary,
            cancellationToken);

        return BackendApiMapper.ToVectorDbSummary(response);
    }

    public Task<StudyDocumentsResponse> GetStudyDocumentsAsync(string studyId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new StudyDocumentsResponse(studyId, []));

    public async Task<StartIngestionWorkflowResponse> StartIngestionWorkflowAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<IngestionWorkflowStatusResponse>(
            RdKnowledgeBackendRoutes.StartIngestion,
            new StartIngestionRequest { SourceId = sourceId },
            cancellationToken);

        return new StartIngestionWorkflowResponse(
            response.ExecutionId,
            response.SourceId,
            BackendApiMapper.ParseWorkflowStatus(response.Status));
    }

    public async Task<IngestionWorkflowProgress> GetIngestionStatusAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<IngestionWorkflowStatusResponse>(
            RdKnowledgeBackendRoutes.GetIngestionStatus.Replace("{executionId}", Uri.EscapeDataString(executionId)),
            cancellationToken);

        var study = scenarios.ResolveIngestionScenario(response.SourceId)?.Study;
        return BackendApiMapper.ToIngestionProgress(response, study);
    }

    public async Task<SubmitIngestionDecisionResponse> SubmitIngestionDecisionAsync(
        string executionId,
        string sourceId,
        SubmitIngestionDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = RdKnowledgeBackendRoutes.SubmitIngestionDecision
            .Replace("{sourceId}", Uri.EscapeDataString(sourceId))
            .Replace("{executionId}", Uri.EscapeDataString(executionId));

        var response = await PostAsync<IngestionWorkflowStatusResponse>(
            path,
            new WorkflowApprovalRequest
            {
                Approved = request.Approved,
                ReviewerComment = request.Notes
            },
            cancellationToken);

        return new SubmitIngestionDecisionResponse(
            response.ExecutionId,
            BackendApiMapper.ParseWorkflowStatus(response.Status));
    }

    public Task<QuerySessionState> GetQuerySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var cached = querySessionCache.TryGet(sessionId)
            ?? querySessionCache.GetOrCreate(sessionId);
        return Task.FromResult(BackendApiMapper.ToQuerySessionState(cached));
    }

    public async Task<QuerySessionState> SendChatMessageAsync(
        string sessionId,
        SendChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var cached = querySessionCache.GetOrCreate(sessionId, request.StudyScope);
        if (!string.IsNullOrWhiteSpace(request.StudyScope))
        {
            cached.StudyScope = request.StudyScope;
        }

        cached.Messages.Add(new ChatMessage(
            "user",
            request.Question,
            null,
            null,
            null,
            DateTimeOffset.UtcNow));

        var response = await PostAsync<ChatAnswerResponse>(
            RdKnowledgeBackendRoutes.Ask,
            new AskRequest { SessionId = sessionId, Question = request.Question },
            cancellationToken);

        cached.Messages.Add(BackendApiMapper.ToAssistantMessage(response));
        cached.CurateEnabled = response.CurateEnabled;

        return BackendApiMapper.ToQuerySessionState(cached);
    }

    public async Task<StartCurationResponse> StartCurationAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<CurateWorkflowStatusResponse>(
            RdKnowledgeBackendRoutes.StartCurate,
            new StartCurateRequest { SessionId = sessionId },
            cancellationToken);

        querySessionCache.SetCurationExecutionId(sessionId, response.ExecutionId);

        return new StartCurationResponse(
            response.ExecutionId,
            response.SessionId,
            BackendApiMapper.ParseWorkflowStatus(response.Status));
    }

    public async Task<CurationWorkflowProgress> GetCurationStatusAsync(
        string curationExecutionId,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<CurateWorkflowStatusResponse>(
            RdKnowledgeBackendRoutes.GetCurateStatus.Replace("{executionId}", Uri.EscapeDataString(curationExecutionId)),
            cancellationToken);

        return BackendApiMapper.ToCurationProgress(response);
    }

    public async Task<SubmitQueryDecisionResponse> SubmitCurationDecisionAsync(
        string sessionId,
        string curationExecutionId,
        SubmitQueryDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = RdKnowledgeBackendRoutes.SubmitCurateDecision
            .Replace("{sessionId}", Uri.EscapeDataString(sessionId))
            .Replace("{executionId}", Uri.EscapeDataString(curationExecutionId));

        var response = await PostAsync<CurateWorkflowStatusResponse>(
            path,
            new WorkflowApprovalRequest
            {
                Approved = request.Approved,
                ReviewerComment = request.Notes
            },
            cancellationToken);

        return new SubmitQueryDecisionResponse(
            response.ExecutionId,
            BackendApiMapper.ParseWorkflowStatus(response.Status));
    }

    public async Task<bool> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(RdKnowledgeBackendRoutes.Health, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        await ApiProblemDetails.EnsureSuccessOrThrowAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException($"Empty response from {path}.");
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(path, body, JsonOptions, cancellationToken);
        await ApiProblemDetails.EnsureSuccessOrThrowAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException($"Empty response from {path}.");
    }
}

public static class ApiProblemDetails
{
    public static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string message;
        try
        {
            using var doc = JsonDocument.Parse(body);
            message = doc.RootElement.TryGetProperty("detail", out var detail)
                ? detail.GetString() ?? response.ReasonPhrase ?? "Request failed."
                : response.ReasonPhrase ?? "Request failed.";
        }
        catch
        {
            message = response.ReasonPhrase ?? "Request failed.";
        }

        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
