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
using System;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace KrantenJongen.Functions;

[FunctionsStartup(typeof(Startup))]
public class FetchArticlesFunction : IHttpFunction
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
        {
            services
                .AddSingleton(CloudTasksClient.Create())
                .AddSingleton(BigQueryClient.Create(ProjectId.Instance.Id))
                .AddScoped<RunService>()
                .AddScoped<PublishingService>()
                .AddScoped<SourceService>();
        }
    }

    private readonly ILogger<FetchArticlesFunction> _logger;
    private readonly PublishingService _publishingService;
    private readonly RunService _runService;
    private readonly SourceService _sourceService;

    public static readonly string Url = $"https://{RegionId.Instance}-{ProjectId.Instance}.cloudfunctions.net/{nameof(FetchArticlesFunction)}";

    public FetchArticlesFunction(ILogger<FetchArticlesFunction> logger,
        PublishingService publishingService,
        RunService runService,
        SourceService sourceService)
    {
        _logger = logger;
        _publishingService = publishingService;
        _runService = runService;
        _sourceService = sourceService;
    }

    public async Task HandleAsync(HttpContext context)
    {
        await FunctionHelper.HandleAsync(context, Execute);
    }

    private async Task Execute(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching articles");

        var now = DateTime.UtcNow;
        var before = now;
        var after = await _runService.GetLatestRunTimestampAsync() ?? now.AddHours(-1);

        try
        {

            await foreach (var article in _sourceService.FetchArticles(after, before, cancellationToken))
            {
                try
                {
                    await _publishingService.Publish(QueueId.BuildSummary, BuildSummaryFunction.Url, article, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to create task for article: {article.Title}");
                }
            }

            await _runService.InsertRunRecordAsync(Guid.NewGuid().ToString("N"), now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing articles. Timestamp: {Timestamp}, Range: {After} - {Before}", now, after, before);
            throw;
        }
    }
}