using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KrantenJongen.DTO;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace KrantenJongen.Services
{
    public class TelegramService
    {
        private readonly ILogger<TelegramService> _logger;
        private readonly ITelegramBotClient _telegramBotClient;

        public TelegramService(ILogger<TelegramService> logger, ITelegramBotClient telegramBotClient)
        {
            _logger = logger;
            _telegramBotClient = telegramBotClient;
        }

        public async Task Post(Summary summary, CancellationToken cancellationToken)
        {
            try
            {
                // Telegram channel IDs
                var channels = new Dictionary<string, string>
            {
                { "AllNewsEnglish", "@thenetherlandsnews" },
                { "AllNewsRussian", "@thenetherlandsnews_ru" },
                { "NewsHighlightsEnglish", "@thenetherlandsnews_important" },
                { "NewsHighlightsRussian", "@thenetherlandsnews_important_ru" },
                { "GoodVibeNewsEnglish", "@thenetherlandsnews_good" },
                { "GoodVibeNewsRussian", "@thenetherlandsnews_good_ru" }
            };

                // Construct messages
                string englishMessage = CreateMessage(summary, Language.English);
                string russianMessage = CreateMessage(summary, Language.Russian);

                await PostToChannel(channels["AllNewsEnglish"], englishMessage, summary.Media, cancellationToken);
                await PostToChannel(channels["AllNewsRussian"], russianMessage, summary.Media, cancellationToken);

                if (summary.PublishInNewsHighlightsChannel)
                {
                    await PostToChannel(channels["NewsHighlightsEnglish"], englishMessage, summary.Media, cancellationToken);
                    await PostToChannel(channels["NewsHighlightsRussian"], russianMessage, summary.Media, cancellationToken);
                }

                if (summary.PublishInGoodVibeNewsChannel)
                {
                    await PostToChannel(channels["GoodVibeNewsEnglish"], englishMessage, summary.Media, cancellationToken);
                    await PostToChannel(channels["GoodVibeNewsRussian"], russianMessage, summary.Media, cancellationToken);
                }

                _logger.LogInformation("News posted successfully to Telegram channels.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting news to Telegram channels.");
            }
        }

        private string CreateMessage(Summary summary, Language language)
        {
            string aiNotice = language switch
            {
                Language.Russian => "<i>Этот текст был автоматически сгенерирован с использованием технологий ИИ.</i>",
                Language.English => "<i>This text was automatically generated using AI technologies.</i>",
                _ => throw new NotImplementedException()
            };

            string tagsLine = summary.Tags != null && summary.Tags.Any()
                ? string.Join(" ", summary.Tags.Select(tag => $"#{tag}"))
                : string.Empty;

            var content = language switch
            {
                Language.Russian => summary.Russian,
                Language.English => summary.English,
                _ => throw new NotImplementedException()
            };

            return $@"
{tagsLine}

{content}

🔗 <a href=""{summary.Url}"">{summary.Source}</a>

{aiNotice}
";
        }

        private async Task PostToChannel(string channelId, string message, string imageUrl, CancellationToken cancellationToken)
        {
            await _telegramBotClient.SendMessage(
                chatId: channelId,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken
            );

            // DO NOT working as expected because of limitations of the image header length
            // if (!string.IsNullOrEmpty(imageUrl))
            // {
            //     var inputFile = new InputFileUrl(imageUrl);
            //     await _telegramBotClient.SendPhoto(
            //         chatId: channelId,
            //         photo: inputFile,
            //         caption: message,
            //         parseMode: ParseMode.Html,
            //         cancellationToken: cancellationToken
            //     );
            // }
            // else
            // {
            //     await _telegramBotClient.SendMessage(
            //         chatId: channelId,
            //         text: message,
            //         parseMode: ParseMode.Html,
            //         cancellationToken: cancellationToken
            //     );
            // }
        }
    }
}
