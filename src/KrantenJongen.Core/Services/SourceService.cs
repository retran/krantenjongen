using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CodeHollow.FeedReader;
using HtmlAgilityPack;
using KrantenJongen.DTO;
using Microsoft.Extensions.Logging;

namespace KrantenJongen.Services;

public sealed class SourceService
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); // Consider changing to Central European Time if needed
    private readonly ILogger<SourceService> _logger;
    private readonly HttpClient _httpClient = new HttpClient();

    public SourceService(ILogger<SourceService> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<Article> FetchArticles(DateTime after, DateTime before,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching articles between {After} and {Before}", after, before);
        var fetchedArticles = new List<Article>();
        var set = new HashSet<string>();
        foreach (var source in Source.Sources)
        {
            _logger.LogInformation("Fetching articles from source: {Source}", source.Name);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    _logger.LogInformation("Attempt {Attempt} to fetch articles from source: {Source}", attempt + 1, source.Name);
                    var articles = FetchArticlesFromSource(source, after, before, cancellationToken);
                    await foreach (var article in articles)
                    {
                        if (set.Add(article.Url))
                        {
                            var fetchedArticle = article;
                            if (!string.IsNullOrEmpty(source.Selector))
                            {
                                fetchedArticle = await FetchContent(source, article);
                            }
                            fetchedArticles.Add(fetchedArticle);
                        }
                        else
                        {
                            _logger.LogInformation("Duplicate article URL {Url} skipped", article.Url);
                        }
                    }
                    break;
                }
                catch (Exception ex) when (attempt < 2)
                {
                    _logger.LogWarning(ex, "An error occurred while fetching articles from source {Source} on attempt {Attempt}. Retrying...", source.Name, attempt + 1);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while fetching articles from source {Source} on final attempt.", source.Name);
                }
            }
        }

        foreach (var article in fetchedArticles.OrderBy(a => a.PublishedAt))
        {
            yield return article;
        }
    }

    private async IAsyncEnumerable<Article> FetchArticlesFromSource(
        Source source,
        DateTime after,
        DateTime before,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching articles from source URL: {Url}", source.Url);
        int count = 0;
        var feed = await FeedReader.ReadAsync(source.Url, cancellationToken).ConfigureAwait(false);
        foreach (var item in feed.Items)
        {
            var article = BuildArticle(item, source);

            if (article.PublishedAt > after && article.PublishedAt <= before)
            {
                count++;
                yield return article;
            }
        }
        _logger.LogInformation("Fetched {Count} articles from source URL: {Url}", count, source.Url);
    }

    private async Task<Article> FetchContent(Source source, Article article)
    {
        _logger.LogInformation("Fetching content for article URL: {Url}", article.Url);
        try
        {
            var page = await LoadPageAsync(article.Url);
            var htmlDoc = new HtmlDocument();

            htmlDoc.LoadHtml(page);

            var tagsToRemove = new[] { "script", "figure", "path", "svg", "img", "style", "iframe", "noscript", "aside", "footer", "header", "nav", "form", "link", "button", "input" };
            foreach (var tag in tagsToRemove)
            {
                foreach (var node in htmlDoc.DocumentNode.SelectNodes($"//{tag}") ?? Enumerable.Empty<HtmlNode>())
                {
                    node.Remove();
                }
            }

            var contentNode = htmlDoc.DocumentNode.SelectSingleNode(source.Selector);
            if (contentNode != null)
            {
                var content = contentNode.InnerHtml;
                string textOnlyContent = RemoveHtmlTags(content);
                article = article with { Content = textOnlyContent };
                _logger.LogInformation("Content fetched and cleaned for article URL: {Url}", article.Url);
            }
            else
            {
                _logger.LogWarning("Content node not found for selector: {Selector} in URL: {Url}", source.Selector, article.Url);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while fetching content for article with URL: {Url}", article.Url);
        }

        _logger.LogInformation("Article content: {Content}", article.Content);

        return article;
    }

    private static string RemoveHtmlTags(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        return Regex.Replace(content, "<.*?>", string.Empty);
    }

    private Article BuildArticle(FeedItem item, Source source)
    {
        DateTime? dt = null;
        if (item.PublishingDate.HasValue)
        {
            dt = item.PublishingDate.Value;
            bool isNotNederlandsDagblad = source.Name != "Nederlands Dagblad";
            if (dt.Value.Kind == DateTimeKind.Utc && isNotNederlandsDagblad)
            {
                dt = DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc);
            }
            else
            {
                // Ensure correct timezone handling for Nederlands Dagblad
                dt = DateTime.SpecifyKind(dt.Value, DateTimeKind.Unspecified);
                dt = TimeZoneInfo.ConvertTimeToUtc(dt.Value, _timeZone);
            }
        }
        else
        {
            var dateString = item.PublishingDateString;
            if (dateString != null)
            {
                // Parsing date format ‘d MMMM yyyy - HH:mm’ for channels with invalid datetime format (e.g., ‘30 August 2023 - 18:15’)
                dt = DateTime.ParseExact(dateString, "d MMMM yyyy - HH:mm", CultureInfo.InvariantCulture);
                dt = DateTime.SpecifyKind(dt.Value, DateTimeKind.Unspecified);
                dt = TimeZoneInfo.ConvertTimeToUtc(dt.Value, _timeZone);
            }
        }

        (string media, string mediaType) = ExtractEnclosureFromXmlElement(item.SpecificItem.Element);

        string content = item.Content ?? string.Empty;

        return new Article(
                source.Name,
                item.Title ?? string.Empty,
                RemoveHtmlTags(item.Description ?? string.Empty),
                RemoveHtmlTags(content),
                dt.HasValue ? dt.Value : DateTime.MinValue,
                item.Link ?? string.Empty,
                media,
                mediaType
            );
    }

    private async Task<string> LoadPageAsync(string url)
    {
        _logger.LogInformation("Loading page at URL: {Url}", url);
        try
        {
            var pageContent = await _httpClient.GetStringAsync(url);
            _logger.LogInformation("Page loaded successfully at URL: {Url}", url);
            return pageContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while loading the page at {Url}", url);
            return string.Empty;
        }
    }

    private (string mediaUrl, string mediaType) ExtractEnclosureFromXmlElement(XElement element)
    {
        var enclosure = element.Element("enclosure");
        if (enclosure != null)
        {
            var url = enclosure.Attribute("url")?.Value ?? string.Empty;
            var type = enclosure.Attribute("type")?.Value ?? string.Empty;
            return (url, type);
        }

        XNamespace mediaNs = "http://search.yahoo.com/mrss/";
        var mediaContent = element.Element(mediaNs + "content");
        if (mediaContent != null)
        {
            var url = mediaContent.Attribute("url")?.Value ?? string.Empty;
            var type = mediaContent.Attribute("type")?.Value ?? string.Empty;
            return (url, type);
        }

        var mediaThumbnail = element.Element(mediaNs + "thumbnail");
        if (mediaThumbnail != null)
        {
            var url = mediaThumbnail.Attribute("url")?.Value ?? string.Empty;
            var type = "image"; // Assuming thumbnail images are images
            return (url, type);
        }

        var linkElements = element.Elements("link");
        foreach (var link in linkElements)
        {
            var rel = link.Attribute("rel")?.Value;
            if (rel == "enclosure")
            {
                var url = link.Attribute("href")?.Value ?? string.Empty;
                var type = link.Attribute("type")?.Value ?? string.Empty;
                return (url, type);
            }
        }

        var content = element.Element("description")?.Value ?? element.Element("content:encoded")?.Value;
        if (!string.IsNullOrEmpty(content))
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);

            var imgNode = htmlDoc.DocumentNode.SelectSingleNode("//img");
            if (imgNode != null)
            {
                var url = imgNode.GetAttributeValue("src", string.Empty);
                return (url, "image");
            }

            var videoNode = htmlDoc.DocumentNode.SelectSingleNode("//video");
            if (videoNode != null)
            {
                var url = videoNode.GetAttributeValue("src", string.Empty);
                return (url, "video");
            }
        }

        _logger.LogInformation("No media found in the XML element");
        return (string.Empty, string.Empty);
    }
}
