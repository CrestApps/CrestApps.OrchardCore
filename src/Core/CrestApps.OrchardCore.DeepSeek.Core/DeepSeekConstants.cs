namespace CrestApps.OrchardCore.DeepSeek.Core;

public static class DeepSeekConstants
{
    public const string TitleGeneratorSystemMessage =
    """
    - Generate a short topic title about the user prompt.
    - Response using title case.
    - Response must be under 255 characters in length.
    """;

    public const string DeepSeekProviderName = "DeepSeekCloud";

    public const string TechnologyName = "DeepSeek";

    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.DeepSeek";

        public const string Chat = "CrestApps.OrchardCore.DeepSeek.Chat";

        public const string CloudChat = "CrestApps.OrchardCore.DeepSeek.Chat.Cloud";
    }
}
