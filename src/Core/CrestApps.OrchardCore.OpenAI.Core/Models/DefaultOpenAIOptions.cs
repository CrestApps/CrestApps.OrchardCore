namespace CrestApps.OrchardCore.OpenAI.Core.Models;

public class DefaultOpenAIOptions
{
    public float Temperature = 0;

    public int MaxOutputTokens = 800;

    public float TopP = 1;

    public float FrequencyPenalty = 0;

    public float PresencePenalty = 0;

    public int PastMessagesCount = 10;
}
