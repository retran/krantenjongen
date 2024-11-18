using System.Threading;
using System.Threading.Tasks;
using KrantenJongen.DTO;
using Microsoft.Extensions.Logging;

namespace KrantenJongen.Services;

public class SummaryService
{
    private readonly ILogger<SummaryService> _logger;
    private readonly GeminiService _geminiService;
    private readonly PromptService _promptService;

    public SummaryService(
        ILogger<SummaryService> logger,
        GeminiService geminiService, 
        PromptService promptService)
    {
        _logger = logger;
        _geminiService = geminiService;
        _promptService = promptService;
    }

    public async Task<Summary> BuildSummary(Article article, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Building summary for article: {article.Url}");

        var buildSummaryResponse = await _geminiService.Generate<BuildSummaryResponse>(
            _promptService.GetBuildSummarySystemPrompt(),
            _promptService.GetBuildSummaryRequestPrompt(article),
            cancellationToken);

        _logger.LogInformation("Built English summary: {EnglishSummary}", buildSummaryResponse.English);
        _logger.LogInformation("Built Russian summary: {RussianSummary}", buildSummaryResponse.Russian);

        return Summary.From(article, buildSummaryResponse);
    }
}