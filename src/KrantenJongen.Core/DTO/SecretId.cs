namespace KrantenJongen.DTO;

public record SecretId(string Id)
{
    public static readonly SecretId TelegramBotApiKey = new SecretId("TelegramBotApiKey");

    public override string ToString() => Id;
}
