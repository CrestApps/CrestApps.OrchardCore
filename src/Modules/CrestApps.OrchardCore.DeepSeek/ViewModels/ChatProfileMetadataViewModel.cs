using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DeepSeek.ViewModels;

public class ChatProfileMetadataViewModel
{
    public string SystemMessage { get; set; }

    [Range(0f, 2f)]
    public float? Temperature { get; set; }

    [Range(0f, 1f)]
    public float? TopP { get; set; }

    [Range(-2f, 2f)]
    public float? FrequencyPenalty { get; set; }

    [Range(0f, 1f)]
    public float? PresencePenalty { get; set; }

    [Range(2, int.MaxValue)]
    public int? MaxTokens { get; set; }

    [Range(2, 20)]
    public int? PastMessagesCount { get; set; }

    [BindNever]
    public bool IsSystemMessageLocked { get; set; }
}
