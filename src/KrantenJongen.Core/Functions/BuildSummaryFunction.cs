using System.Threading;
using Google.Cloud.Functions.Framework;
using Google.Cloud.Functions.Hosting;
using Google.Cloud.Tasks.V2;
using KrantenJongen.DTO;
using KrantenJongen.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace KrantenJongen.Functions;

[FunctionsStartup(typeof(Startup))]
public class BuildSummaryFunction : IHttpFunction
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
        {
            services
                .AddSingleton(CloudTasksClient.Create())
                .AddSingleton(new PromptService())
                .AddScoped<PublishingService>()
                .AddScoped<GeminiService>()
                .AddScoped<SummaryService>();
        }
    }

    private readonly ILogger<BuildSummaryFunction> _logger;
    private readonly PublishingService _publishingService;
    private readonly SummaryService _summaryService;

    public static readonly string Url = $"https://{RegionId.Instance}-{ProjectId.Instance}.cloudfunctions.net/{nameof(BuildSummaryFunction)}";

    public BuildSummaryFunction(ILogger<BuildSummaryFunction> logger, 
        PublishingService publishingService,
        SummaryService summaryService)
    {
        _logger = logger;
        _publishingService = publishingService;
        _summaryService = summaryService;
    }

    public async Task HandleAsync(HttpContext context)
    {
        await FunctionHelper.HandleAsync<Article>(context, Execute);
    }

    private async Task Execute(Article article, CancellationToken cancellationToken)
    {
        var summary = await _summaryService.BuildSummary(article, cancellationToken);
        await _publishingService.Publish(QueueId.FilterSummary, FilterSummaryFunction.Url, summary, cancellationToken);
    }
}
