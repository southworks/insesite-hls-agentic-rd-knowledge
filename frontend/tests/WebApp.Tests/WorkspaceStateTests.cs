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
    public async Task QueryLoadAsync_WithExecutionId_LoadsCompletedProgressAndDisablesStart()
    {
        const string sessionId = "query-test";
        const string sampleQuestion = "Which protocols share the same endpoint?";
        var (state, simulator, sessionStore) = CreateQueryState();
        sessionStore.OpenQuerySession(sessionId, "Test query", StudyId, sampleQuestion);
        CompleteQuery(simulator, "qry-1", sessionId, sampleQuestion);

        await state.LoadAsync(sessionId, "qry-1");

        Assert.NotNull(state.Progress);
        Assert.Equal(WorkflowStatus.Completed, state.Progress.Status);
        Assert.False(state.CanStartWorkflow);
    }

    [Fact]
    public async Task QueryLoadAsync_WithoutExecutionId_ClearsCompletedProgressAndResetsQuestion()
    {
        const string sessionId = "query-test";
        const string sampleQuestion = "Which protocols share the same endpoint?";
        var (state, simulator, sessionStore) = CreateQueryState();
        sessionStore.OpenQuerySession(sessionId, "Test query", StudyId, sampleQuestion);
        CompleteQuery(simulator, "qry-1", sessionId, sampleQuestion, "Modified during execution");

        await state.LoadAsync(sessionId, "qry-1");
        Assert.Equal("Modified during execution", state.Question);

        await state.LoadAsync(sessionId, null);

        Assert.Null(state.Progress);
        Assert.Equal(sampleQuestion, state.Question);
        Assert.True(state.CanStartWorkflow);
        Assert.False(state.IsPolling);
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
    public void OpenQuerySession_ResetsExecutionMetadata()
    {
        const string sessionId = "query-test";
        var sessionStore = new KnowledgeSessionStore();
        var session = sessionStore.OpenQuerySession(sessionId, "Test query", StudyId, "Question?");
        session.ExecutionId = "qry-old";
        session.Status = WorkflowStatus.Completed;
        sessionStore.UpdateSession(session);

        var reopened = sessionStore.OpenQuerySession(sessionId, "Test query", StudyId, "Question?");

        Assert.Null(reopened.ExecutionId);
        Assert.Equal(WorkflowStatus.Pending, reopened.Status);
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
            Options.Create(new WorkflowPollingOptions()));
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

    private static void CompleteIngestion(MockWorkflowSimulator simulator, string executionId)
    {
        simulator.StartIngestion(executionId, StudyId);
        for (var i = 0; i < 5; i++)
        {
            simulator.AdvanceOnPoll(executionId);
        }

        simulator.SubmitDecision(executionId, true, "Approved");
    }

    private static void CompleteQuery(
        MockWorkflowSimulator simulator,
        string executionId,
        string sessionId,
        string question,
        string? questionOverride = null)
    {
        simulator.StartQuery(executionId, sessionId, questionOverride ?? question, StudyId);
        for (var i = 0; i < 5; i++)
        {
            simulator.AdvanceOnPoll(executionId);
        }

        simulator.SubmitDecision(executionId, true, "Approved");
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
