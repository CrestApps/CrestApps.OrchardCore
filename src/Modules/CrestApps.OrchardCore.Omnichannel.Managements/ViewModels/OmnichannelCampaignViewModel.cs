using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class OmnichannelCampaignViewModel
{
    public string DisplayText { get; set; }

    public string Description { get; set; }

    public ActivityInteractionType InteractionType { get; set; }

    public string ChannelEndpointId { get; set; }

    public string InitialOutboundPromptPattern { get; set; }

    public string Channel { get; set; }

    public string CampaignGoal { get; set; }

    public string ProviderName { get; set; }

    public string ConnectionName { get; set; }

    public string DeploymentName { get; set; }

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

    public Dictionary<string, ToolEntry[]> Tools { get; set; }

    public SelectListItem[] Dispositions { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> InteractionTypes { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ChannelEndpoints { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Providers { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ConnectionNames { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> DeploymentNames { get; set; }
}
