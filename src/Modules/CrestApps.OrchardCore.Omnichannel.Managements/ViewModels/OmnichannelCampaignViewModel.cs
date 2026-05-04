using System.ComponentModel.DataAnnotations;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for omnichannel campaign.
/// </summary>
public class OmnichannelCampaignViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the interaction type.
    /// </summary>
    public ActivityInteractionType InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoint id.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the initial outbound prompt pattern.
    /// </summary>
    public string InitialOutboundPromptPattern { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the campaign goal.
    /// </summary>
    public string CampaignGoal { get; set; }

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the connection name.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the deployment name.
    /// </summary>
    public string DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the system message.
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// Gets or sets the temperature.
    /// </summary>
    [Range(0f, 1f)]
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the top p.
    /// </summary>
    [Range(0f, 1f)]
    public float? TopP { get; set; }

    /// <summary>
    /// Gets or sets the frequency penalty.
    /// </summary>
    [Range(0f, 1f)]
    public float? FrequencyPenalty { get; set; }

    /// <summary>
    /// Gets or sets the presence penalty.
    /// </summary>
    [Range(0f, 1f)]
    public float? PresencePenalty { get; set; }

    /// <summary>
    /// Gets or sets the max tokens.
    /// </summary>
    [Range(4, int.MaxValue)]
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the tools.
    /// </summary>
    public Dictionary<string, ToolEntry[]> Tools { get; set; }

    /// <summary>
    /// Gets or sets the dispositions.
    /// </summary>
    public SelectListItem[] Dispositions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether allow AI to update contact.
    /// </summary>
    public bool AllowAIToUpdateContact { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether allow AI to update subject.
    /// </summary>
    public bool AllowAIToUpdateSubject { get; set; }

    /// <summary>
    /// Gets or sets the interaction types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> InteractionTypes { get; set; }

    /// <summary>
    /// Gets or sets the channels.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoints.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ChannelEndpoints { get; set; }

    /// <summary>
    /// Gets or sets the providers.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Providers { get; set; }

    /// <summary>
    /// Gets or sets the connection names.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ConnectionNames { get; set; }

    /// <summary>
    /// Gets or sets the deployment names.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> DeploymentNames { get; set; }
}
