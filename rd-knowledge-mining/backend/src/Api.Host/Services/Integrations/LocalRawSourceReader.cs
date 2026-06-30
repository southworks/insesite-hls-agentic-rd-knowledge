using CohereRndKnowledgeMining.Api.Host.Options;
using Microsoft.Extensions.Options;

namespace CohereRndKnowledgeMining.Api.Host.Services.Integrations;

public sealed class LocalRawSourceReader : IFabricRawSourceReader
{
    private readonly string _rootPath;

    public LocalRawSourceReader(IOptions<DatasetOptions> options, IHostEnvironment environment)
    {
        _rootPath = ResolveContentPath(environment.ContentRootPath, options.Value.RootPath);
    }

    public Task<IReadOnlyList<RawKnowledgeItem>> ReadAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        var ingestDir = Path.Combine(_rootPath, "cases", sourceId, "ingest");

        if (!Directory.Exists(ingestDir))
        {
            throw new KeyNotFoundException($"Source directory not found: {ingestDir}");
        }

        var files = Directory.EnumerateFiles(ingestDir)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<RawKnowledgeItem>>([]);
        }

        var items = new List<RawKnowledgeItem>(files.Count);
        for (int i = 0; i < files.Count; i++)
        {
            var filePath = files[i];
            var fileName = Path.GetFileName(filePath);

            var content = File.ReadAllText(filePath).Trim();

            items.Add(new RawKnowledgeItem
            {
                ItemId = $"{sourceId}-{i + 1:D3}",
                Title = fileName,
                SourceType = RawSourceTypeInference.InferSourceType(fileName),
                Content = content,
                SourcePath = filePath
            });
        }

        return Task.FromResult<IReadOnlyList<RawKnowledgeItem>>(items);
    }

    private static string ResolveContentPath(string contentRootPath, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(contentRootPath, path));
    }
}
