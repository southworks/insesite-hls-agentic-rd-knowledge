namespace Cohere.AgenticRDKnowledge.Shared.Contracts;

public enum WorkflowStatus
{
    Pending,
    Running,
    AwaitingHumanApproval,
    Completed,
    Failed
}

public enum WorkflowBlock
{
    Ingestion,
    Query
}

public enum IngestionStage
{
    Pending,
    IngestionTranslation,
    MetadataLinking,
    HumanApproval,
    Completed,
    Failed
}

public enum QueryStage
{
    Pending,
    SearchChat,
    CurationCompliance,
    HumanApproval,
    Completed,
    Failed
}
