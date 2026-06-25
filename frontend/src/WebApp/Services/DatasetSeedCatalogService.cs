using System.Text.Json;
using Cohere.AgenticRDKnowledge.Shared.Contracts;
using Cohere.AgenticRDKnowledge.Shared.Contracts.Studies;
using Cohere.AgenticRDKnowledge.WebApp.Configuration;
using Cohere.AgenticRDKnowledge.WebApp.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Cohere.AgenticRDKnowledge.WebApp.Services;

public sealed class DatasetSeedCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _rootPath;
    private IReadOnlyList<SeedScenarioDefinition>? _scenarios;
    private IReadOnlyDictionary<string, StudyDocumentsResponse>? _documentsByStudy;

    public DatasetSeedCatalogService(IOptions<DatasetSeedOptions> options, IHostEnvironment environment)
    {
        var configuredPath = options.Value.RootPath;
        _rootPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
    }

    public IReadOnlyList<SeedScenarioDefinition> GetScenarios()
    {
        _scenarios ??= LoadScenarios();
        return _scenarios;
    }

    public IReadOnlyList<SeedScenarioDefinition> GetIngestionScenarios() =>
        GetScenarios().Where(s => s.Block.Equals("Ingestion", StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<SeedScenarioDefinition> GetQueryScenarios() =>
        GetScenarios().Where(s => s.Block.Equals("Query", StringComparison.OrdinalIgnoreCase)).ToList();

    public SeedScenarioDefinition? GetScenario(string scenarioId) =>
        GetScenarios().FirstOrDefault(s => s.ScenarioId.Equals(scenarioId, StringComparison.OrdinalIgnoreCase));

    public SeedScenarioDefinition? GetScenarioByStudyId(string studyId, WorkflowBlock block) =>
        GetScenarios().FirstOrDefault(s =>
            s.StudyId.Equals(studyId, StringComparison.OrdinalIgnoreCase) &&
            s.Block.Equals(block.ToString(), StringComparison.OrdinalIgnoreCase));

    public StudyDocumentsResponse GetStudyDocuments(string studyId)
    {
        _documentsByStudy ??= LoadDocuments();
        if (_documentsByStudy.TryGetValue(studyId, out var docs))
        {
            return docs;
        }

        var scenario = GetScenarios().FirstOrDefault(s => s.StudyId.Equals(studyId, StringComparison.OrdinalIgnoreCase));
        return new StudyDocumentsResponse(studyId, []);
    }

    public string? ReadAgentOutputJson(string studyId, string fileName)
    {
        var path = Path.Combine(_rootPath, "studies", studyId, "agent-outputs", fileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private IReadOnlyList<SeedScenarioDefinition> LoadScenarios()
    {
        var scenariosDir = Path.Combine(_rootPath, "scenarios");
        if (!Directory.Exists(scenariosDir))
        {
            return [];
        }

        var results = new List<SeedScenarioDefinition>();
        foreach (var file in Directory.GetFiles(scenariosDir, "*.json"))
        {
            var json = File.ReadAllText(file);
            var scenario = JsonSerializer.Deserialize<SeedScenarioDefinition>(json, JsonOptions);
            if (scenario is not null)
            {
                results.Add(scenario);
            }
        }

        return results;
    }

    private IReadOnlyDictionary<string, StudyDocumentsResponse> LoadDocuments()
    {
        var map = new Dictionary<string, StudyDocumentsResponse>(StringComparer.OrdinalIgnoreCase);
        var studiesDir = Path.Combine(_rootPath, "studies");
        if (!Directory.Exists(studiesDir))
        {
            return map;
        }

        foreach (var studyDir in Directory.GetDirectories(studiesDir))
        {
            var manifestPath = Path.Combine(studyDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var json = File.ReadAllText(manifestPath);
            var docs = JsonSerializer.Deserialize<StudyDocumentsResponse>(json, JsonOptions);
            if (docs is not null)
            {
                map[docs.StudyId] = docs;
            }
        }

        return map;
    }
}
