namespace CrestApps.Core.AI.Models;

public class AIProfileMetadata
{
    public string SystemMessage { get; set; }

    public string InitialPrompt { get; set; }

    public float? Temperature { get; set; }

    public float? TopP { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public int? MaxTokens { get; set; }

    public int? PastMessagesCount { get; set; }

    public bool UseCaching { get; set; } = true;
}
