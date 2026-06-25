namespace Cohere.AgenticRDKnowledge.Shared.Contracts.Fabric;

public sealed record FabricStoreSummary(
    int TotalStudies,
    int TotalDocuments,
    int TotalEntities,
    int TotalLinks,
    DateTimeOffset? LastIngestionAt,
    string? LastIngestedStudyId);
