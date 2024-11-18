using Google.Cloud.Tasks.V2;
using Google.Cloud.BigQuery.V2;
using Google.Cloud.Functions.Framework;
using Google.Cloud.Functions.Hosting;
using Google.Cloud.SecretManager.V1;
using KrantenJongen.DTO;
using KrantenJongen.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Task = System.Threading.Tasks.Task;

namespace KrantenJongen.Functions;

[FunctionsStartup(typeof(Startup))]
public class PostSummaryFunction : IHttpFunction
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
        {
            var telegramBotApiKey = AccessSecretAsync(ProjectId.Instance, SecretId.TelegramBotApiKey, "1").Result;
            services
                .AddSingleton(CloudTasksClient.Create())
                .AddSingleton(BigQueryClient.Create(ProjectId.Instance.Id))
                .AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramBotApiKey))
                .AddScoped<PublishingService>()
                .AddScoped<SimilarityService>()
                .AddScoped<GeminiService>()
                .AddScoped<TelegramService>();
        }

        private async Task<string> AccessSecretAsync(ProjectId projectId, SecretId secretId, string secretVersionId)
        {
            var client = await SecretManagerServiceClient.CreateAsync();
            var secretVersionName = new SecretVersionName(projectId.Id, secretId.Id, secretVersionId);
            var result = await client.AccessSecretVersionAsync(secretVersionName);
            return result.Payload.Data.ToStringUtf8();
        }
    }

    private readonly ILogger<PostSummaryFunction> _logger;
    private readonly TelegramService _telegramService;

    public static readonly string Id = $"https://{RegionId.Instance}-{ProjectId.Instance}.cloudfunctions.net/{nameof(PostSummaryFunction)}";

    public PostSummaryFunction(
        ILogger<PostSummaryFunction> logger,
        TelegramService telegramService)
    {
        _logger = logger;
        _telegramService = telegramService;
    }

    public async Task HandleAsync(HttpContext context)
    {
        await FunctionHelper.HandleAsync<Summary>(context, Execute);
    }

    private async Task Execute(Summary summary, CancellationToken cancellationToken)
    {
        await _telegramService.Post(summary, cancellationToken);
    }
}
