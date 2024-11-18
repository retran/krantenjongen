using System;
using System.Collections.Generic;

namespace KrantenJongen.DTO;

public record Summary(
    string Source,
    string English,
    string Russian,
    DateTime PublishedAt,
    string Url,
    string Media,
    string MediaType,
    IEnumerable<string> Tags,
    bool PublishInGoodVibeNewsChannel,
    bool PublishInNewsHighlightsChannel)
{
    public static readonly Summary Empty = new Summary(
        string.Empty,
        string.Empty,
        string.Empty,
        DateTime.MinValue,
        string.Empty,
        string.Empty,
        string.Empty,
        [],
        false,
        false);

    public static Summary From(
        Article article, 
        BuildSummaryResponse buildSummaryResponse)
    {
        return new Summary(article.Source,
                           buildSummaryResponse.English,
                           buildSummaryResponse.Russian,
                           article.PublishedAt,
                           article.Url,
                           article.Media,
                           article.MediaType,
                           buildSummaryResponse.Tags,
                           buildSummaryResponse.PublishInGoodVibeNewsChannel,
                           buildSummaryResponse.PublishInNewsHighlightsChannel);
    }
}
