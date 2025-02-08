namespace CrestApps.OrchardCore.AI.Ollama.Models;

public class OllamaChatProfileMetadata
{
    public string SystemMessage { get; set; }

    public float? Temperature { get; set; }

    public float? TopP { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public int? MaxTokens { get; set; }

    public int? PastMessagesCount { get; set; }
}
