using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class CustomChatInstanceViewModel
{
    public string InstanceId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; }

    public string ConnectionName { get; set; }

    public string DeploymentId { get; set; }

    public string SystemMessage { get; set; }

    [Range(4, int.MaxValue)]
    public int? MaxTokens { get; set; }

    [Range(0f, 1f)]
    public float? Temperature { get; set; }

    [Range(0f, 1f)]
    public float? TopP { get; set; }

    [Range(0f, 1f)]
    public float? FrequencyPenalty { get; set; }

    [Range(0f, 1f)]
    public float? PresencePenalty { get; set; }

    [Range(2, 20)]
    public int? PastMessagesCount { get; set; }

    public string ProviderName { get; set; }

    public Dictionary<string, ToolEntry[]> Tools { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ConnectionNames { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; }

    [BindNever]
    public bool IsNew { get; set; }
}
