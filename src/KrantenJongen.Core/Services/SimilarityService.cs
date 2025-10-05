using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.BigQuery.V2;
using KrantenJongen.DTO;
using Microsoft.Extensions.Logging;

namespace KrantenJongen.Services;

public class SimilarityService
{
    private const string DatasetId = "krantenjongen";
    private const string TableId = "summary_embeddings";
    private const double SimilarityThreshold = 0.8;

    private readonly string _checkForSimilarRecordQuery = $@"
        SELECT
            1
        FROM `{ProjectId.Instance}.{DatasetId}.{TableId}`
        WHERE
            1 - COSINE_DISTANCE(embedding, [{{0}}]) >= {SimilarityThreshold}
        LIMIT 1";
    private readonly string _removeRecordsBeforeDateQuery = $@"
        DELETE FROM `{ProjectId.Instance}.{DatasetId}.{TableId}`
        WHERE created_at < @cutoff_date";


    private readonly BigQueryClient _client;
    private readonly GeminiService _geminiService;
    private readonly ILogger<SimilarityService> _logger;

    public SimilarityService(ILogger<SimilarityService> logger, BigQueryClient client, GeminiService geminiService)
    {
        _logger = logger;
        _client = client;
        _geminiService = geminiService;
    }

    public async Task RemoveRecordsBeforeDateAsync(DateTime cutoffDate)
    {
        _logger.LogInformation("Initiating removal of records before {CutoffDate}", cutoffDate);
        try
        {
            var parameters = new[]
            {
                new BigQueryParameter("cutoff_date", BigQueryDbType.Timestamp, cutoffDate)
            };

            await _client.ExecuteQueryAsync(_removeRecordsBeforeDateQuery, parameters);
            _logger.LogInformation("Successfully removed records before {CutoffDate}", cutoffDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting records before {CutoffDate} from BigQuery", cutoffDate);
        }    
    }

    public async Task<bool> AddSummaryIfUniqueAsync(Summary summary)
    {
        _logger.LogInformation("Processing summary for URL: {Url}", summary.Url);
        try
        {
            var embedding = await _geminiService.GetEmbedding(summary.English);

            _logger.LogInformation("Generated embedding for URL: {Url}", summary.Url);

            if (await CheckForSimilarRecordAsync(embedding))
            {
                _logger.LogInformation("Similar record found for URL: {Url}. Skipping insertion.", summary.Url);
                return false;
            }

            await InsertRecordAsync(summary.Url, summary.English, summary.PublishedAt, embedding);
            
            _logger.LogInformation("Inserted new summary record for URL: {Url}", summary.Url);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing summary for URL: {Url}", summary.Url);
            return false;
        }
    }

    private async Task InsertRecordAsync(string id, string summary, DateTime publishedAt, float[] embedding)
    {
        _logger.LogInformation("Inserting record with ID: {Id}", id);
        try
        {
            string publishedAtFormatted = publishedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss");
            var table = _client.GetTable(DatasetId, TableId);
            var record = new BigQueryInsertRow
            {
                { "id", id },
                { "summary", summary },
                { "published_at", publishedAtFormatted },
                { "embedding", embedding }
            };
            await table.InsertRowAsync(record);
            _logger.LogInformation("Successfully inserted record with ID: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding record with ID: {Id} to BigQuery", id);
            throw new Exception($"Error adding record with ID: {id} to BigQuery", ex);
        }
    }
    private async Task<bool> CheckForSimilarRecordAsync(float[] newVector)
    {
        _logger.LogInformation("Checking for similar records in BigQuery");
        try
        {
            var newVectorString = string.Join(",", newVector);
            string query = string.Format(_checkForSimilarRecordQuery, newVectorString);
            var result = await _client.ExecuteQueryAsync(query, []);
            bool exists = result.Any();
            _logger.LogInformation("Similar record {Existence}found in BigQuery", exists ? "" : "not ");
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for similar records in BigQuery");
            return false;
        }
    }
}
