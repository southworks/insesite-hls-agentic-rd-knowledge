namespace RndKnowledgeMining.Mcp.Options;

public sealed class FabricLakehouseOptions
{
    public const string SectionName = "FabricLakehouse";

    public string WorkspaceName { get; set; } = string.Empty;

    public string LakehouseName { get; set; } = string.Empty;

    public string RawRoot { get; set; } = "Files/raw";

    public string NormalizedRoot { get; set; } = "Files/normalized";

    public int TimeoutSeconds { get; set; } = 30;
}
