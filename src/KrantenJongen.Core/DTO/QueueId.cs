using System;
using Google.Cloud.Tasks.V2;

namespace KrantenJongen.DTO;

public record QueueId(string Id)
{
    public static readonly QueueId BuildSummary = new QueueId("buildSummary");
    public static readonly QueueId FilterSummary = new QueueId("filterSummary");
    public static readonly QueueId PostSummary = new QueueId("postSummary");

    public QueueName ToQueueName()
    {
        if (ProjectId.Instance == null)
        {
            throw new InvalidOperationException("ProjectId.Instance is null");
        }
        if (RegionId.Instance == null)
        {
            throw new InvalidOperationException("RegionId.Instance is null");
        }
        return new QueueName(ProjectId.Instance.Id, RegionId.Instance.Id, Id);
    }

    public override string ToString() => Id;
}