using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using Google.Cloud.AIPlatform.V1;
using KrantenJongen.DTO;

namespace KrantenJongen.Services;

public class PromptService
{
    private readonly ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();

    private string LoadResource(string resource)
    {
        if (!_cache.ContainsKey(resource))
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"KrantenJongen.Prompts.{resource}";

            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new FileNotFoundException($"Resource '{resourceName}' not found.");
            }

            using (var reader = new StreamReader(stream))
            {
                _cache.AddOrUpdate(resource, reader.ReadToEnd(), (key, oldValue) => reader.ReadToEnd());
            }
        }

        return _cache[resource];
    }

    public string GetBuildSummarySystemPrompt()
    {
        return LoadResource("build-summary-system.prompt");
    }

    public string GetBuildSummaryRequestPrompt(Article article)
    {
        var promptTemplate = LoadResource("build-summary-request.prompt");
        return string.Format(
            promptTemplate,
            article.Title,
            article.PublishedAt,
            article.Description,
            article.Content,
            article.Source);
    }

    public string GetBuildSummarySchema()
    {
        return LoadResource("build-summary.json");
    }

    public OpenApiSchema GetBuildSummarySchemaForGoogleApi()
    {
        return new OpenApiSchema
        {
            Type = Type.Object,
            Description = "Schema representing a translation, summaries, tags, assessments, and publishing decisions.",
            Properties =
            {
                // 01_dutch_to_english_translation
                {
                    "01_dutch_to_english_translation",
                    new OpenApiSchema
                    {
                        Type = Type.String,
                        Description = "Full English translation. Use standard capitalization and do not include any headlines or titles."
                    }
                },
                // 02_english_summary
                {
                    "02_english_summary",
                    new OpenApiSchema
                    {
                        Type = Type.String,
                        Description = "Concise English summary (150-200 words) in paragraph form. Do not include headlines or use all caps; follow standard capitalization rules."
                    }
                },
                // 03_rewritten_english_summary
                {
                    "03_rewritten_english_summary",
                    new OpenApiSchema
                    {
                        Type = Type.String,
                        Description = "Summary rewritten to meet professional journalistic standards, maintaining standard capitalization and excluding any headlines."
                    }
                },
                // 04_russian_translation
                {
                    "04_russian_translation",
                    new OpenApiSchema
                    {
                        Type = Type.String,
                        Description = "Russian translation of the rewritten English summary, using standard capitalization and no headlines."
                    }
                },
                // 05_rewritten_russian_translation
                {
                    "05_rewritten_russian_translation",
                    new OpenApiSchema
                    {
                        Type = Type.String,
                        Description = "Russian translation rewritten to journalistic standards, ensuring standard capitalization and the exclusion of headlines."
                    }
                },
                // 06_tags
                {
                    "06_tags",
                    new OpenApiSchema
                    {
                        Type = Type.Array,
                        Items = new OpenApiSchema
                        {
                            Type = Type.String
                        },
                        Description = "Array of relevant one-word tags."
                    }
                },
                // 07_good_vibe_assessment
                {
                    "07_good_vibe_assessment",
                    new OpenApiSchema
                    {
                        Type = Type.Object,
                        Properties =
                        {
                            {
                                "01_positive_impact",
                                new OpenApiSchema
                                {
                                    Type = Type.Object,
                                    Properties =
                                    {
                                        {
                                            "uplifting_and_inspiring",
                                            new OpenApiSchema
                                            {
                                                Type = Type.String,
                                                Enum = { "Yes", "No" }
                                            }
                                        },
                                        {
                                            "promotes_good_vibes",
                                            new OpenApiSchema
                                            {
                                                Type = Type.String,
                                                Enum = { "Yes", "No" }
                                            }
                                        },
                                        {
                                            "avoids_negative_content",
                                            new OpenApiSchema
                                            {
                                                Type = Type.String,
                                                Enum = { "Yes", "No" }
                                            }
                                        }
                                    },
                                    Required = { "uplifting_and_inspiring", "promotes_good_vibes", "avoids_negative_content" }
                                }
                            },
                            {
                                "02_relevance_and_value",
                                new OpenApiSchema
                                {
                                    Type = Type.Object,
                                    Properties =
                                    {
                                        {
                                            "fits_good_vibe_categories",
                                            new OpenApiSchema
                                            {
                                                Type = Type.Array,
                                                Items = new OpenApiSchema
                                                {
                                                    Type = Type.String
                                                }
                                            }
                                        },
                                        {
                                            "provides_practical_value",
                                            new OpenApiSchema
                                            {
                                                Type = Type.String,
                                                Enum = { "Yes", "No" }
                                            }
                                        },
                                        {
                                            "relevant_to_expat_life",
                                            new OpenApiSchema
                                            {
                                                Type = Type.String,
                                                Enum = { "Yes", "No" }
                                            }
                                        }
                                    },
                                    Required = { "fits_good_vibe_categories", "provides_practical_value", "relevant_to_expat_life" }
                                }
                            },
                            {
                                "03_engagement_potential",
                                new OpenApiSchema
                                {
                                    Type = Type.Object,
                                    Properties =
                                    {
                                        {
                                            "encourages_community_spirit",
                                            new OpenApiSchema
                                            {
                                                Type = Type.String,
                                                Enum = { "Yes", "No" }
                                            }
                                        },
                                        {
                                            "inspires_and_motivates",
                                            new OpenApiSchema
                                            {
                                                Type = Type.String,
                                                Enum = { "Yes", "No" }
                                            }
                                        },
                                        {
                                            "contains_actionable_information",
                                            new OpenApiSchema
                                            {
                                                Type = Type.String,
                                                Enum = { "Yes", "No" }
                                            }
                                        }
                                    },
                                    Required = { "encourages_community_spirit", "inspires_and_motivates", "contains_actionable_information" }
                                }
                            },
                            {
                                "04_final_verdict",
                                new OpenApiSchema
                                {
                                    Type = Type.Object,
                                    Properties =
                                    {
                                        {
                                            "01_publish_in_good_vibe_news_space",
                                            new OpenApiSchema
                                            {
                                                Type = Type.String,
                                                Enum = { "Yes", "No" }
                                            }
                                        },
                                        {
                                            "02_confidence_level",
                                            new OpenApiSchema
                                            {
                                                Type = Type.String,
                                                Enum = { "High", "Medium", "Low" }
                                            }
                                        },
                                        {
                                            "03_explanation",
                                            new OpenApiSchema
                                            {
                                                Type = Type.String
                                            }
                                        }
                                    },
                                    Required = { "01_publish_in_good_vibe_news_space", "02_confidence_level", "03_explanation" }
                                }
                            }
                        },
                        Required = { "01_positive_impact", "02_relevance_and_value", "03_engagement_potential", "04_final_verdict" },
                        Description = "Evaluation of content suitability for the good vibe news space."
                    }
                },
                // 08_publish_in_good_vibe_news_channel
                {
                    "08_publish_in_good_vibe_news_channel",
                    new OpenApiSchema
                    {
                        Type = Type.Boolean,
                        Description = "`true` or `false` indicating the final verdict for the good vibe news space."
                    }
                },
                // 09_news_highlights_assessment
                {
                    "09_news_highlights_assessment",
                    new OpenApiSchema
                    {
                        Type = Type.Object,
                        Properties =
                        {
                            // Здесь вы можете добавить оставшиеся свойства по аналогии
                        },
                        Description = "Evaluation of the news' importance for the news highlights channel."
                    }
                },
                // 10_publish_in_news_highlights_channel
                {
                    "10_publish_in_news_highlights_channel",
                    new OpenApiSchema
                    {
                        Type = Type.Boolean,
                        Description = "`true` or `false` indicating the final verdict for the news highlights channel."
                    }
                }
            },
            Required =
            {
                "01_dutch_to_english_translation",
                "02_english_summary",
                "03_rewritten_english_summary",
                "04_russian_translation",
                "05_rewritten_russian_translation",
                "06_tags",
                "07_good_vibe_assessment",
                "08_publish_in_good_vibe_news_channel",
                "09_news_highlights_assessment",
                "10_publish_in_news_highlights_channel"
            }
        };
    }
}