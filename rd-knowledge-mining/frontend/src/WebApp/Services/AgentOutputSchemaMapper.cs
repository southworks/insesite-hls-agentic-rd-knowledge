using System.Text.Json;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Agents;

namespace Cohere.AgenticRDKnowledge.WebApp.Services;

/// <summary>
/// Maps live Api.Host agent structured-output JSON into UI DTOs used by Blazor panels.
/// </summary>
public static class AgentOutputSchemaMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IngestionTranslationResult? MapIngestionTranslation(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("summary", out var summaryProp))
        {
            return null;
        }

        var summary = summaryProp.GetString();
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        var keyFacts = ReadStringArray(root, "keyFacts");
        var anomalies = ReadStringArray(root, "anomalies");
        var flags = ReadStringArray(root, "flags");

        return new IngestionTranslationResult(
            summary,
            keyFacts.Count,
            anomalies.Count,
            ["JSON", "XML", "TXT"],
            flags);
    }

    public static MetadataLinkingResult? MapMetadataLinking(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("summary", out var summaryProp))
        {
            return null;
        }

        var summary = summaryProp.GetString();
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        var entities = new List<EntityChip>();
        if (root.TryGetProperty("entities", out var entitiesProp) && entitiesProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var entity in entitiesProp.EnumerateArray())
            {
                var name = entity.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var category = entity.TryGetProperty("category", out var catProp) ? catProp.GetString() ?? "—" : "—";
                var version = entity.TryGetProperty("version", out var verProp) ? verProp.GetString() ?? "—" : "—";
                entities.Add(new EntityChip(name, category, version));
            }
        }

        var links = new List<DocumentLink>();
        if (root.TryGetProperty("links", out var linksProp) && linksProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in linksProp.EnumerateArray())
            {
                var from = link.TryGetProperty("fromDocument", out var fromProp) ? fromProp.GetString() : null;
                var to = link.TryGetProperty("toTarget", out var toProp) ? toProp.GetString() : null;
                var relationship = link.TryGetProperty("relationship", out var relProp) ? relProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                {
                    continue;
                }

                links.Add(new DocumentLink(from, to, relationship ?? "linked"));
            }
        }

        var vectorsIndexed = root.TryGetProperty("vectorsIndexed", out var vectorsProp) && vectorsProp.TryGetInt32(out var count)
            ? count
            : 0;

        return new MetadataLinkingResult(summary, entities, links, vectorsIndexed);
    }

    public static CurationComplianceResult? MapCurationCompliance(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("summary", out var summaryProp))
        {
            return null;
        }

        var summary = summaryProp.GetString();
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        var flagLabels = ReadStringArray(root, "flags");
        var policyRefs = ReadStringArray(root, "policyRefs");
        var capturedDecisions = ReadStringArray(root, "capturedDecisions");

        var complianceFlags = new List<ComplianceFlag>();
        for (int i = 0; i < flagLabels.Count; i++)
        {
            complianceFlags.Add(new ComplianceFlag(
                "Review",
                "Compliance",
                flagLabels[i],
                i < policyRefs.Count ? policyRefs[i] : null));
        }

        return new CurationComplianceResult(summary, complianceFlags, capturedDecisions);
    }

    public static SearchChatResult? MapSearchChat(string json)
    {
        var legacy = JsonSerializer.Deserialize<SearchChatResult>(json, JsonOptions);
        if (legacy is not null && !string.IsNullOrWhiteSpace(legacy.Answer))
        {
            return legacy;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var answer = root.TryGetProperty("answer", out var answerProp) ? answerProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = root.TryGetProperty("summary", out var summaryProp) ? summaryProp.GetString() : null;
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            return null;
        }

        var citations = ReadStringArray(root, "citations")
            .Select((citation, index) => new Citation(
                $"cite-{index + 1}",
                citation,
                citation,
                "Vector DB",
                1.0))
            .ToList();

        var lineage = root.TryGetProperty("evidence", out var evidenceProp) ? evidenceProp.GetString() : null;

        return new SearchChatResult(answer, citations, lineage ?? string.Empty);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var arrayProp) || arrayProp.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return arrayProp.EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
    }
}
