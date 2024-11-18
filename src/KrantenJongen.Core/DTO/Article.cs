using System;

namespace KrantenJongen.DTO;

public record Article(
    string Source,
    string Title,
    string Description,
    string Content,
    DateTime PublishedAt,
    string Url,
    string Media,
    string MediaType)
{
    public static readonly Article Empty = new Article(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        DateTime.MinValue,
        string.Empty,
        string.Empty,
        string.Empty);
}
