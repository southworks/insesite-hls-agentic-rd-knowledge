namespace Cohere.AgenticRDKnowledge.Shared.Contracts.Agents;

public sealed record IngestionTranslationResult(
    string Summary,
    int DocumentsProcessed,
    int DuplicatesRemoved,
    IReadOnlyList<string> NormalizedFormats,
    IReadOnlyList<string> ConnectedPortals);

public sealed record MetadataLinkingResult(
    string Summary,
    IReadOnlyList<EntityChip> Entities,
    IReadOnlyList<DocumentLink> Links,
    int VectorsIndexed);

public sealed record EntityChip(string Name, string Category, string Version);

public sealed record DocumentLink(string FromDocument, string ToTarget, string Relationship);

public sealed record SearchChatResult(
    string Answer,
    IReadOnlyList<Citation> Citations,
    string LineageSummary);

public sealed record Citation(
    string DocumentId,
    string Title,
    string Excerpt,
    string SourceSystem,
    double RelevanceScore);

public sealed record CurationComplianceResult(
    string Summary,
    IReadOnlyList<ComplianceFlag> Flags,
    IReadOnlyList<string> PromptedOwners);

public sealed record ComplianceFlag(
    string Severity,
    string Category,
    string Description,
    string? PolicyReference);

public sealed record RetrievalTraceEvent(
    string Stage,
    string Description,
    int ItemCount,
    DateTimeOffset Timestamp);

public sealed record HumanDecisionRecord(
    bool Approved,
    string? Notes,
    DateTimeOffset DecidedAt);
