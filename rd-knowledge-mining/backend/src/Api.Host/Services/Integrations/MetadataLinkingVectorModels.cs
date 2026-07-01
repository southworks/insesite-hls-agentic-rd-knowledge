using System.Text.Json;
using System.Text.Json.Serialization;

namespace CohereRndKnowledgeMining.Api.Host.Services.Integrations;

public sealed class MetadataLinkingVectorOutput
{
    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;

    [JsonPropertyName("decision")]
    public string Decision { get; init; } = string.Empty;

    [JsonPropertyName("evidence")]
    public string Evidence { get; init; } = string.Empty;

    [JsonPropertyName("entities")]
    public IReadOnlyList<MetadataLinkedEntity> Entities { get; init; } = [];

    [JsonPropertyName("links")]
    public IReadOnlyList<MetadataEntityLink> Links { get; init; } = [];

    [JsonPropertyName("entityIds")]
    public IReadOnlyList<string> EntityIds { get; init; } = [];

    [JsonPropertyName("vectorsIndexed")]
    public int VectorsIndexed { get; init; }

    public static MetadataLinkingVectorOutput Parse(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new InvalidOperationException("Metadata-linking output was empty. Expected structured JSON output.");
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            MetadataLinkingVectorOutput? parsed = JsonSerializer.Deserialize<MetadataLinkingVectorOutput>(rawJson, options);
            if (parsed is null)
            {
                throw new InvalidOperationException("Metadata-linking output could not be parsed.");
            }

            return parsed;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Metadata-linking output is not valid JSON: {Truncate(rawJson)}",
                exception);
        }
    }

    private static string Truncate(string text)
    {
        const int maxLength = 220;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}

public sealed class MetadataLinkedEntity
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
}

public sealed class MetadataEntityLink
{
    [JsonPropertyName("fromDocument")]
    public string FromDocument { get; init; } = string.Empty;

    [JsonPropertyName("toTarget")]
    public string ToTarget { get; init; } = string.Empty;

    [JsonPropertyName("relationship")]
    public string Relationship { get; init; } = string.Empty;
}
