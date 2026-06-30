using Cohere.AgenticRDKnowledge.WebApp.Services;

namespace Cohere.AgenticRDKnowledge.WebApp.Tests;

public sealed class AgentOutputParserTests
{
    private const string AgentStructuredOutputJson = """
        {
          "summary": "Synthetic ELN/LIMS records normalized and linked to GEO series GSE323366.",
          "decision": "approve",
          "evidence": "Two synthetic samples associated with public GEO structure.",
          "keyFacts": ["SYN-LIMS-001", "SYN-LIMS-010"],
          "flags": ["synthetic_provenance_required", "GSE323366"],
          "policyRefs": ["HLS-DATA-110"]
        }
        """;

    [Fact]
    public void ParseIngestionTranslation_maps_agent_structured_output_with_non_null_collections()
    {
        var result = AgentOutputParser.ParseIngestionTranslation(AgentStructuredOutputJson);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.Summary));
        Assert.NotNull(result.NormalizedFormats);
        Assert.NotNull(result.ConnectedPortals);
        Assert.Equal(2, result.DocumentsProcessed);
        Assert.Equal(2, result.ConnectedPortals.Count);
    }

    [Fact]
    public void ParseMetadataLinking_maps_agent_structured_output_with_non_null_collections()
    {
        var result = AgentOutputParser.ParseMetadataLinking(AgentStructuredOutputJson);

        Assert.NotNull(result);
        Assert.NotNull(result!.Entities);
        Assert.NotNull(result.Links);
        Assert.Equal(2, result.Entities.Count);
        Assert.True(result.Links.Count > 0);
        Assert.Equal("SYN-LIMS-001", result.Entities[0].Name);
    }

    [Fact]
    public void ParseIngestionTranslation_does_not_return_legacy_partial_deserialize()
    {
        var result = AgentOutputParser.ParseIngestionTranslation(AgentStructuredOutputJson);

        Assert.NotNull(result);
        Assert.NotNull(result!.NormalizedFormats);
        Assert.NotNull(result.ConnectedPortals);
    }

    [Fact]
    public void ParseCurationCompliance_maps_agent_structured_output_with_non_null_flags()
    {
        var result = AgentOutputParser.ParseCurationCompliance(AgentStructuredOutputJson);

        Assert.NotNull(result);
        Assert.NotNull(result!.Flags);
        Assert.Equal(2, result.Flags.Count);
    }

    [Fact]
    public void ParseIngestionTranslation_strips_assistant_prefix_and_markdown_fence()
    {
        var wrapped = """
            [assistant]
            ```json
            {"summary":"test","decision":"approve","evidence":"ok","keyFacts":["doc-1"],"flags":["portal-a"]}
            ```
            """;

        var result = AgentOutputParser.ParseIngestionTranslation(wrapped);

        Assert.NotNull(result);
        Assert.Equal("test", result!.Summary);
        Assert.NotNull(result.ConnectedPortals);
    }
}
