using Google.Cloud.BigQuery.V2;
using KrantenJongen.DTO;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace KrantenJongen.Services;

public class RunService
{
    private const string DatasetId = "krantenjongen";
    private const string TableId = "runs";

    private readonly string _getLatestRunTimestampQuery = $@"
        SELECT
            timestamp
        FROM `{ProjectId.Instance}.{DatasetId}.{TableId}`
        ORDER BY timestamp DESC
        LIMIT 1";

    private readonly BigQueryClient _client;
    private readonly ILogger<RunService> _logger;

    public RunService(ILogger<RunService> logger, BigQueryClient client)
    {
        _logger = logger;
        _client = client;
    }

    public async Task InsertRunRecordAsync(string runId, DateTime timestamp)
    {
        _logger.LogInformation("Inserting run record with ID: {RunId}", runId);
        try
        {
            var timestampUtc = timestamp.ToUniversalTime();
            string timestampFormatted = timestampUtc.ToString("yyyy-MM-ddTHH:mm:ss");

            var table = _client.GetTable(DatasetId, TableId);
            var record = new BigQueryInsertRow
            {
                { "run_id", runId },
                { "timestamp", timestampFormatted }
            };
            await table.InsertRowAsync(record);
            _logger.LogInformation("Successfully inserted run record with ID: {RunId}", runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding run record with ID: {RunId} to BigQuery", runId);
            throw;
        }
    }

    public async Task<DateTime?> GetLatestRunTimestampAsync()
    {
        _logger.LogInformation("Fetching latest run timestamp from BigQuery");
        try
        {
            var result = await _client.ExecuteQueryAsync(_getLatestRunTimestampQuery, []);
            var row = result.FirstOrDefault();
            if (row != null)
            {
                if (row["timestamp"] is string timestampStr && DateTime.TryParse(timestampStr, out var timestamp))
                {
                    return timestamp;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching latest run timestamp from BigQuery");
        }
        return null;
    }
}