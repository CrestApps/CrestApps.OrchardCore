using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

public class AICompletionWithConfigTaskViewModel
{
    public string ProviderName { get; set; }

    public string ConnectionName { get; set; }

    public string DeploymentName { get; set; }

    public string PromptTemplate { get; set; }

    public string ResultPropertyName { get; set; }

    public string SystemMessage { get; set; }

    [Range(0f, 1f)]
    public float? Temperature { get; set; }

    [Range(0f, 1f)]
    public float? TopP { get; set; }

    [Range(0f, 1f)]
    public float? FrequencyPenalty { get; set; }

    [Range(0f, 1f)]
    public float? PresencePenalty { get; set; }

    [Range(4, int.MaxValue)]
    public int? MaxTokens { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Providers { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ConnectionNames { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> DeploymentNames { get; set; }
}
