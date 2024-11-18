using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using KrantenJongen.DTO;

namespace KrantenJongen.Services;

public class PromptService
{
    private readonly ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();

    private string LoadResource(string resource)
    {
        if (!_cache.ContainsKey(resource))
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"KrantenJongen.Prompts.{resource}";

            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new FileNotFoundException($"Resource '{resourceName}' not found.");
            }

            using (var reader = new StreamReader(stream))
            {
                _cache.AddOrUpdate(resource, reader.ReadToEnd(), (key, oldValue) => reader.ReadToEnd());
            }
        }

        return _cache[resource];
    }

    public string GetBuildSummarySystemPrompt()
    {
        return LoadResource("build-summary-system.prompt");
    }

    public string GetBuildSummarySchema()
    {
        return LoadResource("build-summary.json");
    }

    public string GetBuildSummaryRequestPrompt(Article article)
    {
        var promptTemplate = LoadResource("build-summary-request.prompt");
        return string.Format(
            promptTemplate,
            article.Title,
            article.PublishedAt,
            article.Description,
            article.Content,
            article.Source);
    }
}