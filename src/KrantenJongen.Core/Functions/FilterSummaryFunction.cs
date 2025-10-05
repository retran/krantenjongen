using System.Threading;
using Google.Cloud.BigQuery.V2;
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
public class FilterSummaryFunction : IHttpFunction
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
        {
            services
                .AddSingleton(CloudTasksClient.Create())
                .AddSingleton(BigQueryClient.Create(ProjectId.Instance.Id))
                .AddScoped<PublishingService>()
                .AddScoped<SimilarityService>()
                .AddScoped<GeminiService>();
        }
    }
    
    private readonly ILogger<FilterSummaryFunction> _logger;
    private readonly SimilarityService _similarityService;
    private readonly PublishingService _publishingService;

    public static readonly string Url = $"https://{RegionId.Instance}-{ProjectId.Instance}.cloudfunctions.net/{nameof(FilterSummaryFunction)}";

    public FilterSummaryFunction(ILogger<FilterSummaryFunction> logger,
        SimilarityService similarityService,
        PublishingService publishingService)
    {
        _logger = logger;
        _similarityService = similarityService;
        _publishingService = publishingService;
    }

    public async Task HandleAsync(HttpContext context)
    {
        await FunctionHelper.HandleAsync<Summary>(context, Execute);
    }

    private async Task Execute(Summary summary, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Filtering summary for URL: {Url}", summary.Url);

        if (await _similarityService.AddSummaryIfUniqueAsync(summary))
        {
            _logger.LogInformation("Summary is unique. Publishing...");
            await _publishingService.Publish(QueueId.PostSummary, PostSummaryFunction.Id, summary, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Summary is not unique. Skipping publishing.");
        }

        _logger.LogInformation("Summary processing complete for URL: {Url}", summary.Url);
    }
}
