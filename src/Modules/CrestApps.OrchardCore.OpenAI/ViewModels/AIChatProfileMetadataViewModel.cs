using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class AIChatProfileMetadataViewModel
{
    [Range(0f, 1f)]
    public float? Temperature { get; set; }

    [Range(0f, 1f)]
    public float? TopP { get; set; }

    [Range(0f, 1f)]
    public float? FrequencyPenalty { get; set; }

    [Range(0f, 1f)]
    public float? PresencePenalty { get; set; }

    [Range(4, 2048)]
    public int? MaxTokens { get; set; }

    [Range(2, 20)]
    public int? PastMessagesCount { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; }
}
