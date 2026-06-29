using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.WebApp.Configuration;
using Cohere.AgenticRDKnowledge.WebApp.Services;
using Cohere.AgenticRDKnowledge.WebApp.State;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Cohere.AgenticRDKnowledge.WebApp.Tests;

public class WorkspaceStateTests
{
    private const string StudyId = "abc-2024";

    [Fact]
    public async Task IngestionLoadAsync_WithExecutionId_LoadsCompletedProgressAndDisablesStart()
    {
        var (state, simulator, _) = CreateIngestionState();
        CompleteIngestion(simulator, "exec-1");

        await state.LoadAsync(StudyId, "exec-1");

        Assert.NotNull(state.Progress);
        Assert.Equal(WorkflowStatus.Completed, state.Progress.Status);
        Assert.False(state.CanStartWorkflow);
    }

    [Fact]
    public async Task IngestionLoadAsync_WithoutExecutionId_ClearsCompletedProgressAndEnablesStart()
    {
        var (state, simulator, _) = CreateIngestionState();
        CompleteIngestion(simulator, "exec-1");

        await state.LoadAsync(StudyId, "exec-1");
        await state.LoadAsync(StudyId, null);

        Assert.Null(state.Progress);
        Assert.True(state.CanStartWorkflow);
        Assert.False(state.IsPolling);
    }

    [Fact]
    public async Task QueryLoadAsync_WithoutExecutionId_ClearsSessionState()
    {
        const string sessionId = "query-test";
        var (state, simulator, sessionStore) = CreateQueryState();
        var executionId = StartQueryRun(simulator, sessionStore, sessionId);

        await state.LoadAsync(sessionId, executionId);
        await state.LoadAsync(sessionId, null);

        Assert.Null(state.Session);
        Assert.Null(state.CurationProgress);
        Assert.False(state.IsChatPolling);
        Assert.False(state.IsCurationPolling);
    }

    [Fact]
    public async Task SendMessage_AddsChatWithoutStartingCuration()
    {
        const string sessionId = "query-test";
        var (state, simulator, sessionStore) = CreateQueryState();
        var executionId = StartQueryRun(simulator, sessionStore, sessionId);

        await state.LoadAsync(sessionId, executionId);
        state.SetQuestion("Which protocols share the same endpoint?");
        await state.SendMessageAsync();

        for (var i = 0; i < 3; i++)
        {
            await state.LoadAsync(sessionId, executionId);
            if (state.Session?.IsChatRunning != true)
            {
                break;
            }

            await Task.Delay(50);
        }

        Assert.NotNull(state.Session);
        Assert.True(state.Session.Messages.Count >= 2);
        Assert.Null(state.CurationProgress);
        Assert.False(state.CanStartCuration == false && state.Session.Messages.Count == 0);
    }

    [Fact]
    public async Task StartCuration_RequiresChatMessages()
    {
        const string sessionId = "query-test";
        var (state, simulator, sessionStore) = CreateQueryState();
        var executionId = StartQueryRun(simulator, sessionStore, sessionId);

        await state.LoadAsync(sessionId, executionId);
        Assert.False(state.CanStartCuration);

        state.SetQuestion("Which protocols share the same endpoint?");
        await state.SendMessageAsync();

        for (var i = 0; i < 5; i++)
        {
            await state.LoadAsync(sessionId, executionId);
            if (state.Session?.IsChatRunning != true && state.Session?.Messages.Count >= 2)
            {
                break;
            }

            await Task.Delay(50);
        }

        Assert.True(state.CanStartCuration);
        await state.StartCurationAsync();

        Assert.NotNull(state.ExecutionId);
        Assert.NotNull(state.CurationProgress);
    }

    [Fact]
    public async Task CurationDecision_CompletesWithoutBlockingChat()
    {
        const string sessionId = "query-curate";
        var (state, simulator, sessionStore) = CreateQueryState();
        var executionId = StartQueryRun(simulator, sessionStore, sessionId);
        CompleteChatAndCuration(simulator, executionId, "Which protocols share the endpoint?");

        await state.LoadAsync(sessionId, executionId);

        Assert.NotNull(state.CurationProgress);
        Assert.Equal(WorkflowStatus.AwaitingHumanApproval, state.CurationProgress.Status);
        Assert.True(state.CanSubmitDecision);

        await state.SubmitDecisionAsync(true, "Approved");

        Assert.Equal(WorkflowStatus.Completed, state.CurationProgress!.Status);
    }

    [Fact]
    public void OpenIngestionSession_ResetsExecutionMetadata()
    {
        var sessionStore = new KnowledgeSessionStore();
        var session = sessionStore.OpenIngestionSession(StudyId, "Study ABC");
        session.ExecutionId = "exec-old";
        session.Status = WorkflowStatus.Completed;
        sessionStore.UpdateSession(session);

        var reopened = sessionStore.OpenIngestionSession(StudyId, "Study ABC");

        Assert.Null(reopened.ExecutionId);
        Assert.Equal(WorkflowStatus.Pending, reopened.Status);
    }

    [Fact]
    public void OpenQuerySession_CreatesDistinctEntriesPerExecution()
    {
        const string sessionId = "query-test";
        var sessionStore = new KnowledgeSessionStore();
        var first = sessionStore.OpenQuerySession(sessionId, "exec-1", "Test query", StudyId, "Question?");
        first.ChatMessageCount = 4;
        first.Status = WorkflowStatus.Completed;
        sessionStore.UpdateSession(first);

        var second = sessionStore.OpenQuerySession(sessionId, "exec-2", "Test query", StudyId, "Question?");

        Assert.Equal(2, sessionStore.GetSummaries().Count(s => s.Block == WorkflowBlock.Query));
        Assert.Equal("exec-2", second.ExecutionId);
        Assert.Equal(0, second.ChatMessageCount);
        Assert.Equal(WorkflowStatus.Pending, second.Status);
    }

    private static (IngestionWorkspaceState State, MockWorkflowSimulator Simulator, MockRdKnowledgeApiClient Api) CreateIngestionState()
    {
        var (simulator, api, sessionStore) = CreateDependencies();
        var state = new IngestionWorkspaceState(
            api,
            sessionStore,
            Options.Create(new WorkflowPollingOptions()));
        return (state, simulator, api);
    }

    private static (QueryWorkspaceState State, MockWorkflowSimulator Simulator, KnowledgeSessionStore SessionStore) CreateQueryState()
    {
        var (simulator, api, sessionStore) = CreateDependencies();
        var state = new QueryWorkspaceState(
            api,
            sessionStore,
            Options.Create(new WorkflowPollingOptions { IntervalSeconds = 1 }));
        return (state, simulator, sessionStore);
    }

    private static (MockWorkflowSimulator Simulator, MockRdKnowledgeApiClient Api, KnowledgeSessionStore SessionStore) CreateDependencies()
    {
        var catalog = new DatasetSeedCatalogService(
            Options.Create(new DatasetSeedOptions { RootPath = "../../dataset-seed" }),
            new TestHostEnvironment(GetWebAppContentRoot()));
        var simulator = new MockWorkflowSimulator();
        var api = new MockRdKnowledgeApiClient(catalog, simulator);
        var sessionStore = new KnowledgeSessionStore();
        return (simulator, api, sessionStore);
    }

    private static string StartQueryRun(
        MockWorkflowSimulator simulator,
        KnowledgeSessionStore sessionStore,
        string sessionId)
    {
        var executionId = $"qry-{Guid.NewGuid():N}"[..12];
        simulator.StartQueryWorkflow(executionId, sessionId, StudyId);
        sessionStore.OpenQuerySession(sessionId, executionId, "Test query", StudyId, "Question?");
        return executionId;
    }

    private static void CompleteIngestion(MockWorkflowSimulator simulator, string executionId)
    {
        simulator.StartIngestion(executionId, StudyId);
        for (var i = 0; i < 5; i++)
        {
            simulator.AdvanceOnPoll(executionId);
        }

        simulator.SubmitDecision(executionId, true, "Approved");
    }

    private static void CompleteChatAndCuration(
        MockWorkflowSimulator simulator,
        string executionId,
        string question)
    {
        simulator.BeginChatTurn(executionId, question, StudyId);
        simulator.AdvanceChatOnPoll(executionId);
        simulator.AdvanceChatOnPoll(executionId);

        simulator.StartCuration(executionId);
        for (var i = 0; i < 3; i++)
        {
            simulator.AdvanceOnPoll(executionId);
        }
    }

    private static string GetWebAppContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var webApp = Path.Combine(dir.FullName, "src", "WebApp");
            if (Directory.Exists(webApp))
            {
                return webApp;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("WebApp content root not found.");
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "WebApp.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
