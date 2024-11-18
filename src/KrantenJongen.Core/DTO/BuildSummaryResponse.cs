namespace KrantenJongen.DTO;

using System.Collections.Generic;
using Newtonsoft.Json;

public record BuildSummaryResponse(
    [property: JsonProperty("03_rewritten_english_summary")] string English,
    [property: JsonProperty("05_rewritten_russian_translation")] string Russian,
    [property: JsonProperty("06_tags")] List<string> Tags,
    [property: JsonProperty("08_publish_in_good_vibe_news_channel")] bool PublishInGoodVibeNewsChannel,
    [property: JsonProperty("10_publish_in_news_highlights_channel")] bool PublishInNewsHighlightsChannel);
