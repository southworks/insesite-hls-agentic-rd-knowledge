using System.Net.Http.Json;
using System.Text.Json;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Ingestion;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Query;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Studies;
using Cohere.AgenticRDKnowledge.Shared.Contracts.VectorDb;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Backend;

namespace Cohere.AgenticRDKnowledge.WebApp.Services;

public interface IRdKnowledgeApiClient
{
    Task<VectorDbStoreSummary> GetVectorDbStoreSummaryAsync(CancellationToken cancellationToken = default);
    Task<StudyDocumentsResponse> GetStudyDocumentsAsync(string studyId, CancellationToken cancellationToken = default);
    Task<StartIngestionWorkflowResponse> StartIngestionWorkflowAsync(string studyId, CancellationToken cancellationToken = default);
    Task<IngestionWorkflowProgress> GetIngestionStatusAsync(string executionId, CancellationToken cancellationToken = default);
    Task<SubmitIngestionDecisionResponse> SubmitIngestionDecisionAsync(string executionId, SubmitIngestionDecisionRequest request, CancellationToken cancellationToken = default);
    Task<StartQueryWorkflowResponse> StartQueryWorkflowAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<QuerySessionState> GetQuerySessionAsync(string executionId, CancellationToken cancellationToken = default);
    Task<QuerySessionState> SendChatMessageAsync(string executionId, SendChatMessageRequest request, CancellationToken cancellationToken = default);
    Task<StartCurationResponse> StartCurationAsync(string executionId, CancellationToken cancellationToken = default);
    Task<CurationWorkflowProgress> GetCurationStatusAsync(string curationExecutionId, CancellationToken cancellationToken = default);
    Task<SubmitQueryDecisionResponse> SubmitCurationDecisionAsync(string curationExecutionId, SubmitQueryDecisionRequest request, CancellationToken cancellationToken = default);
    Task<bool> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed class RdKnowledgeApiClient(HttpClient httpClient) : IRdKnowledgeApiClient
{
    public Task<VectorDbStoreSummary> GetVectorDbStoreSummaryAsync(CancellationToken cancellationToken = default) =>
        GetAsync<VectorDbStoreSummary>(RdKnowledgeBackendRoutes.GetVectorDbStoreSummary, cancellationToken);

    public Task<StudyDocumentsResponse> GetStudyDocumentsAsync(string studyId, CancellationToken cancellationToken = default) =>
        GetAsync<StudyDocumentsResponse>(
            RdKnowledgeBackendRoutes.GetStudyDocuments.Replace("{studyId}", Uri.EscapeDataString(studyId)),
            cancellationToken);

    public Task<StartIngestionWorkflowResponse> StartIngestionWorkflowAsync(string studyId, CancellationToken cancellationToken = default) =>
        PostAsync<StartIngestionWorkflowResponse>(
            RdKnowledgeBackendRoutes.StartIngestionWorkflow.Replace("{studyId}", Uri.EscapeDataString(studyId)),
            null,
            cancellationToken);

    public Task<IngestionWorkflowProgress> GetIngestionStatusAsync(string executionId, CancellationToken cancellationToken = default) =>
        GetAsync<IngestionWorkflowProgress>(
            RdKnowledgeBackendRoutes.GetIngestionStatus.Replace("{executionId}", Uri.EscapeDataString(executionId)),
            cancellationToken);

    public Task<SubmitIngestionDecisionResponse> SubmitIngestionDecisionAsync(
        string executionId,
        SubmitIngestionDecisionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SubmitIngestionDecisionResponse>(
            RdKnowledgeBackendRoutes.SubmitIngestionDecision.Replace("{executionId}", Uri.EscapeDataString(executionId)),
            request,
            cancellationToken);

    public Task<StartQueryWorkflowResponse> StartQueryWorkflowAsync(string sessionId, CancellationToken cancellationToken = default) =>
        PostAsync<StartQueryWorkflowResponse>(
            RdKnowledgeBackendRoutes.StartQueryWorkflow.Replace("{sessionId}", Uri.EscapeDataString(sessionId)),
            null,
            cancellationToken);

    public Task<QuerySessionState> GetQuerySessionAsync(string executionId, CancellationToken cancellationToken = default) =>
        GetAsync<QuerySessionState>(
            RdKnowledgeBackendRoutes.GetQuerySession.Replace("{executionId}", Uri.EscapeDataString(executionId)),
            cancellationToken);

    public Task<QuerySessionState> SendChatMessageAsync(string executionId, SendChatMessageRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<QuerySessionState>(
            RdKnowledgeBackendRoutes.SendChatMessage.Replace("{executionId}", Uri.EscapeDataString(executionId)),
            request,
            cancellationToken);

    public Task<StartCurationResponse> StartCurationAsync(string executionId, CancellationToken cancellationToken = default) =>
        PostAsync<StartCurationResponse>(
            RdKnowledgeBackendRoutes.StartCuration.Replace("{executionId}", Uri.EscapeDataString(executionId)),
            null,
            cancellationToken);

    public Task<CurationWorkflowProgress> GetCurationStatusAsync(string curationExecutionId, CancellationToken cancellationToken = default) =>
        GetAsync<CurationWorkflowProgress>(
            RdKnowledgeBackendRoutes.GetCurationStatus.Replace("{executionId}", Uri.EscapeDataString(curationExecutionId)),
            cancellationToken);

    public Task<SubmitQueryDecisionResponse> SubmitCurationDecisionAsync(
        string curationExecutionId,
        SubmitQueryDecisionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SubmitQueryDecisionResponse>(
            RdKnowledgeBackendRoutes.SubmitCurationDecision.Replace("{executionId}", Uri.EscapeDataString(curationExecutionId)),
            request,
            cancellationToken);

    public async Task<bool> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(RdKnowledgeBackendRoutes.Health, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        await ApiProblemDetails.EnsureSuccessOrThrowAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        return result ?? throw new InvalidOperationException($"Empty response from {path}.");
    }

    private async Task<T> PostAsync<T>(string path, object? body, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = body is null
            ? await httpClient.PostAsync(path, null, cancellationToken)
            : await httpClient.PostAsJsonAsync(path, body, cancellationToken);
        await ApiProblemDetails.EnsureSuccessOrThrowAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
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
