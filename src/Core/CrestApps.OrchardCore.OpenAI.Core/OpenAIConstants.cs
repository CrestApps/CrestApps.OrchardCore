namespace CrestApps.OrchardCore.OpenAI.Core;

public static class OpenAIConstants
{
    public const string TitleGeneratorSystemMessage =
    """
    - Generate a short topic title about the user prompt.
    - Response using title case.
    - Response must be under 255 characters in length.
    """;

    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.OpenAI";

        public const string ChatGPT = "CrestApps.OrchardCore.OpenAI.ChatGPT";
    }
}
