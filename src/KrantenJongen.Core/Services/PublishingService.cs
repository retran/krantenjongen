using System;
using System.Threading;
using Google.Cloud.Tasks.V2;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using KrantenJongen.DTO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Task = System.Threading.Tasks.Task;

namespace KrantenJongen.Services;

public class PublishingService
{
    private readonly ILogger<PublishingService> _logger;
    private readonly CloudTasksClient _tasksClient;

    public PublishingService(
        ILogger<PublishingService> logger,
        CloudTasksClient tasksClient)
    {
        _logger = logger;
        _tasksClient = tasksClient;
    }

    public async Task Publish<T>(QueueId queueId, string functionUrl, T data, CancellationToken cancellationToken)
    {
        var payload = JsonConvert.SerializeObject(data);

        _logger.LogInformation("Publishing to {QueueId} with payload: {Payload} and function URL: {FunctionUrl}", queueId, payload, functionUrl);
        
        var task = new Google.Cloud.Tasks.V2.Task
        {
            HttpRequest = new HttpRequest
            {
                HttpMethod = HttpMethod.Post,
                Url = functionUrl,
                Body = ByteString.CopyFromUtf8(payload),
                Headers =
                {
                    { "Content-Type", "application/json" }
                },
            },
            ScheduleTime = Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime())
        };

        var retryCount = 3;
        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                await _tasksClient.CreateTaskAsync(
                    queueId.ToQueueName(),
                    task,
                    cancellationToken);
                break;
            }
            catch (Exception ex) when (i < retryCount - 1)
            {
                _logger.LogWarning(ex, "Failed to publish to {QueueId}, retrying... ({Attempt}/{MaxAttempts})", queueId, i + 1, retryCount);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish to {QueueId} after {MaxAttempts} attempts", queueId, retryCount);
                throw;
            }
        }

        _logger.LogInformation("Published to {QueueId}", queueId);
    }
}