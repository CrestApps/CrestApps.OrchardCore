using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureAIChatProfileMetadata
{
    public string SystemMessage { get; set; }

    [Range(0f, 1f)]
    public float? Temperature { get; set; }

    [Range(0f, 1f)]
    public float? TopP { get; set; }

    [Range(0f, 1f)]
    public float? FrequencyPenalty { get; set; }

    [Range(0f, 1f)]
    public float? PresencePenalty { get; set; }

    [Range(4, 2048)]
    public int? TokenLength { get; set; }

    [Range(2, 20)]
    public int? PastMessagesCount { get; set; }
}