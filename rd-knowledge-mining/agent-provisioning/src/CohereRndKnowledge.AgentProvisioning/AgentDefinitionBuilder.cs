using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CohereRndKnowledge.AgentProvisioning.Models;

namespace CohereRndKnowledge.AgentProvisioning;

public sealed class AgentDefinitionBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    public string BuildDefinitionJson(AgentAssetBundle bundle, ProvisioningSettings settings)
    {
        string serverUrl = $"{settings.McpBaseUrl.TrimEnd('/')}{NormalizePath(bundle.Mcp.Path)}";

        JsonObject definition = new()
        {
            ["kind"] = "prompt",
            ["model"] = settings.ModelDeploymentName,
            ["instructions"] = BuildInstructions(bundle),
            ["temperature"] = 0.2,
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mcp",
                    ["server_label"] = bundle.Mcp.ServerLabel,
                    ["server_url"] = serverUrl,
                    ["require_approval"] = "never",
                    ["headers"] = new JsonObject
                    {
                        ["X-Agent-Role"] = bundle.Manifest.Name
                    }
                }
            },
            ["text"] = new JsonObject
            {
                ["format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["name"] = "AgentStructuredOutput",
                    ["description"] = "Structured agent output consumed by the R&D knowledge mining workflow API.",
                    ["schema"] = JsonNode.Parse(bundle.OutputSchemaJson),
                    ["strict"] = true
                }
            }
        };

        return definition.ToJsonString(SerializerOptions);
    }

    public string BuildCreateVersionRequestJson(string definitionJson, AgentManifest manifest)
    {
        JsonObject request = new()
        {
            ["definition"] = JsonNode.Parse(definitionJson),
            ["description"] = manifest.Description
        };

        return request.ToJsonString(SerializerOptions);
    }

    public string ComputeFingerprint(AgentAssetBundle bundle, string definitionJson)
    {
        StringBuilder builder = new();
        builder.Append(definitionJson);
        builder.Append('\n');
        builder.Append(bundle.GovernancePolicyYaml);
        builder.Append('\n');
        builder.Append(bundle.GovernanceRogueYaml);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }

    private static string BuildInstructions(AgentAssetBundle bundle)
    {
        StringBuilder builder = new();
        builder.AppendLine(bundle.Instructions);
        builder.AppendLine();
        builder.AppendLine("## Structured Output Contract");
        if (UsesSearchChatStructuredOutput(bundle))
        {
            builder.AppendLine("Return JSON only with these required properties: summary, decision, evidence, citations, lineage, raw_source_trace.");
            builder.AppendLine("The API requires all of these properties.");
            builder.AppendLine();
            builder.AppendLine("Formatting rules:");
            builder.AppendLine("- Return raw JSON only. Do not wrap the JSON in markdown code fences.");
            builder.AppendLine("- Do not include extra text before or after the JSON.");
            builder.AppendLine("- evidence must be a plain string, not an object or array.");
            builder.AppendLine("- citations must be an array of strings. Use an empty array when none apply.");
            builder.AppendLine("- lineage must be a plain string. Use an empty string when traceability is not relevant.");
            builder.AppendLine("- raw_source_trace must be true only when the answer is grounded in persisted knowledge-base entities.");
        }
        else if (UsesCurationComplianceStructuredOutput(bundle))
        {
            builder.AppendLine("Return JSON only with these required properties: summary, decision, evidence, flags, capturedDecisions, policyRefs, citations, sensitive_content_found, required_human_review.");
            builder.AppendLine("The API requires all of these properties.");
            builder.AppendLine();
            builder.AppendLine("Formatting rules:");
            builder.AppendLine("- Return raw JSON only. Do not wrap the JSON in markdown code fences.");
            builder.AppendLine("- Do not include extra text before or after the JSON.");
            builder.AppendLine("- evidence must be a plain string, not an object or array.");
            builder.AppendLine("- flags, capturedDecisions, policyRefs, and citations must be arrays of strings. Use empty arrays when none apply.");
            builder.AppendLine("- sensitive_content_found and required_human_review must be booleans.");
        }
        else if (UsesIngestionTranslationStructuredOutput(bundle))
        {
            builder.AppendLine("Return JSON only with these required properties: summary, decision, evidence, anomalies, keyFacts, documentsProcessed, duplicatesRemoved, normalizedFormats, normalizedDocuments.");
            builder.AppendLine("The API requires all of these properties.");
            builder.AppendLine();
            builder.AppendLine("Formatting rules:");
            builder.AppendLine("- Return raw JSON only. Do not wrap the JSON in markdown code fences.");
            builder.AppendLine("- Do not include extra text before or after the JSON.");
            builder.AppendLine("- summary, decision, and evidence must be plain strings.");
            builder.AppendLine("- anomalies, keyFacts, normalizedFormats must be arrays of strings. Use empty arrays when none apply.");
            builder.AppendLine("- documentsProcessed and duplicatesRemoved must be integers.");
            builder.AppendLine("- normalizedDocuments must be an array of objects with required properties: documentId, sourceItemId, sourceType, title, canonicalKey, status. Use an empty array when no documents are accepted.");
        }
        else if (UsesMetadataLinkingStructuredOutput(bundle))
        {
            builder.AppendLine("Return JSON only with these required properties: summary, decision, evidence, entities, links, entityIds, vectorsIndexed.");
            builder.AppendLine("The API requires all of these properties.");
            builder.AppendLine();
            builder.AppendLine("Formatting rules:");
            builder.AppendLine("- Return raw JSON only. Do not wrap the JSON in markdown code fences.");
            builder.AppendLine("- Do not include extra text before or after the JSON.");
            builder.AppendLine("- summary, decision, and evidence must be plain strings.");
            builder.AppendLine("- entities must be an array of objects with required properties: name, category, version.");
            builder.AppendLine("- links must be an array of objects with required properties: fromDocument, toTarget, relationship.");
            builder.AppendLine("- entityIds must be an array of strings. Use an empty array when none apply.");
            builder.AppendLine("- vectorsIndexed must be an integer.");
        }
        else
        {
            builder.AppendLine("Return JSON only with these required properties: summary, decision, evidence.");
            builder.AppendLine("The API requires summary, decision, and evidence.");
            builder.AppendLine();
            builder.AppendLine("Formatting rules:");
            builder.AppendLine("- Return raw JSON only. Do not wrap the JSON in markdown code fences.");
            builder.AppendLine("- Do not include extra text before or after the JSON.");
            builder.AppendLine("- evidence must be a plain string, not an object or array.");
        }
        builder.AppendLine();
        builder.AppendLine("## Allowed Decision Values");
        foreach (string decision in bundle.Manifest.AllowedDecisions)
        {
            builder.AppendLine($"- {decision}");
        }

        builder.AppendLine();
        builder.AppendLine("## Workflow Boundaries");
        builder.AppendLine("Consume prior workflow outputs when provided. Do not repeat work owned by earlier agents.");
        builder.AppendLine("Produce recommendations and evidence only. Human-in-the-loop orchestration is handled by the workflow, not by this agent.");

        return builder.ToString().Trim();
    }

    private static bool UsesSearchChatStructuredOutput(AgentAssetBundle bundle) =>
        bundle.OutputSchemaJson.Contains("\"raw_source_trace\"", StringComparison.Ordinal);

    private static bool UsesCurationComplianceStructuredOutput(AgentAssetBundle bundle) =>
        bundle.OutputSchemaJson.Contains("\"sensitive_content_found\"", StringComparison.Ordinal);

    private static bool UsesIngestionTranslationStructuredOutput(AgentAssetBundle bundle) =>
        bundle.OutputSchemaJson.Contains("\"normalizedDocuments\"", StringComparison.Ordinal);

    private static bool UsesMetadataLinkingStructuredOutput(AgentAssetBundle bundle) =>
        bundle.OutputSchemaJson.Contains("\"entities\"", StringComparison.Ordinal);

    private static string NormalizePath(string path)
    {
        return path.StartsWith('/') ? path : $"/{path}";
    }
}
