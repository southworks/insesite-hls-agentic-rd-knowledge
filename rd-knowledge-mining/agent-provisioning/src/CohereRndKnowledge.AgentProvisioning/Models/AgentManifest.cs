namespace CohereRndKnowledge.AgentProvisioning.Models;

public sealed class AgentManifest
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string InstructionsFile { get; init; }

    public required string OutputSchemaFile { get; init; }

    public required IReadOnlyList<string> AllowedDecisions { get; init; }

    public string GovernancePolicyFile { get; init; } = "governance.yaml";

    public string GovernanceRogueFile { get; init; } = "rogue.yaml";
}
