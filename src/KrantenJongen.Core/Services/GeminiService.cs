using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Google.Cloud.AIPlatform.V1;
using KrantenJongen.DTO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Value = Google.Protobuf.WellKnownTypes.Value;

namespace KrantenJongen.Services;

public class GeminiService
{
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(ILogger<GeminiService> logger)
    {
        _logger = logger;
    }

    public async Task<float[]> GetEmbedding(string text, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Generating embedding in GetEmbedding for text: {text}");

        try
        {
            var client = new PredictionServiceClientBuilder
            {
                Endpoint = $"{RegionId.Instance}-aiplatform.googleapis.com"
            }.Build();

            var endpoint = EndpointName.FromProjectLocationPublisherModel(
                ProjectId.Instance.ToString(),
                RegionId.Instance.ToString(),
                "google",
                "text-embedding-005"
            );

            var instances = new List<Value>
            {
                Value.ForStruct(new()
                {
                    Fields =
                    {
                        ["content"] = Value.ForString(text),
                    }
                })
            };

            var response = await client.PredictAsync(endpoint, instances, null);
            var firstPrediction = response.Predictions.FirstOrDefault();

            if (firstPrediction == null || !firstPrediction.StructValue.Fields.TryGetValue("embeddings", out var embeddings) ||
                !embeddings.StructValue.Fields.TryGetValue("values", out var valuesField))
            {
                throw new InvalidOperationException("Invalid response structure.");
            }

            return valuesField.ListValue.Values.Select(value => (float)value.NumberValue).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating embedding for text: {text}");
            throw;
        }
    }

    public async Task<T> Generate<T>(string systemPrompt, string request, OpenApiSchema schema = null, CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogInformation($"Generating content in Generate for request: {request}");
        string resultPayload = string.Empty;
        try
        {
            var predictionServiceClient = new PredictionServiceClientBuilder
            {
                Endpoint = $"{RegionId.Instance}-aiplatform.googleapis.com"
            }.Build();

            var generateContentRequest = new GenerateContentRequest
            {
                GenerationConfig = new GenerationConfig()
                {
                    ResponseSchema = schema,
                    ResponseMimeType = "application/json"
                },
                SystemInstruction = new Content
                {
                    Role = "system",
                    Parts =
                    {
                        new Part { Text = systemPrompt }
                    }
                },
                Model = $"projects/{ProjectId.Instance}/locations/{RegionId.Instance}/publishers/google/models/gemini-1.5-flash-002",
                Contents =
                {
                    new Content
                    {
                        Role = "user",
                        Parts =
                        {
                            new Part { Text = request }
                        }
                    }
                }
            };

            var response = await predictionServiceClient.GenerateContentAsync(generateContentRequest);
            var candidate = response.Candidates.FirstOrDefault();
            
            if (candidate == null || candidate.Content == null || candidate.Content.Parts == null || candidate.Content.Parts.Count == 0)
            {
                throw new InvalidOperationException("Invalid response structure.");
            }

            resultPayload = candidate.Content.Parts[0].Text;
            _logger.LogInformation($"Generated content: {resultPayload}");
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(resultPayload);
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogError(ex, $"Request is failed: {ex.Status.Detail}");
            throw;
        }
        catch (JsonReaderException ex)
        {
            _logger.LogError(ex, $"Error deserializing response: {resultPayload}");
            throw;
        }
        catch (JsonSerializationException ex)
        {
            _logger.LogError(ex, $"Error deserializing response: {resultPayload}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating content with request: {request}");
            throw;
        }
    }
}