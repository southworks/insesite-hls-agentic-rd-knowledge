using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CohereRndKnowledgeMining.Api.Host.Options;
using Microsoft.Extensions.Options;

namespace CohereRndKnowledgeMining.Api.Host.Services.Integrations;

public sealed class McpKnowledgeIndexingClient : IMetadataLinkingIndexer
{
    private readonly HttpClient _httpClient;
    private readonly McpIntegrationOptions _options;

    public McpKnowledgeIndexingClient(HttpClient httpClient, IOptions<McpIntegrationOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task IndexApprovedMetadataAsync(
        string sourceId,
        string executionId,
        string curatedKnowledgeJson,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.KnowledgeSearchEndpoint))
        {
            throw new InvalidOperationException("McpIntegration:KnowledgeSearchEndpoint is not configured.");
        }

        var callRequest = new McpJsonRpcRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            Method = "tools/call",
            Params = new
            {
                name = "index_rd_knowledge",
                arguments = new
                {
                    sourceId,
                    executionId,
                    curatedKnowledgeJson
                }
            }
        };

        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(_options.KnowledgeSearchEndpoint, callRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string details = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"MCP index tool call failed with status {(int)response.StatusCode}: {details}");
        }

        McpJsonRpcResponse? payload = await response.Content
            .ReadFromJsonAsync<McpJsonRpcResponse>(cancellationToken)
            .ConfigureAwait(false);

        if (payload?.Error is not null)
        {
            throw new InvalidOperationException(
                $"MCP index tool returned error {payload.Error.Code}: {payload.Error.Message}");
        }
    }

    private sealed class McpJsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; init; } = "2.0";

        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("method")]
        public required string Method { get; init; }

        [JsonPropertyName("params")]
        public required object Params { get; init; }
    }

    private sealed class McpJsonRpcResponse
    {
        [JsonPropertyName("error")]
        public McpJsonRpcError? Error { get; init; }
    }

    private sealed class McpJsonRpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; init; }

        [JsonPropertyName("message")]
        public string Message { get; init; } = string.Empty;
    }

}
