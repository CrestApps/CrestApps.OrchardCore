using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.ViewModels;

public sealed class ChatInteractionViewModel
{
    public string Title { get; set; }
    public string ChatDeploymentName { get; set; }
    public string SystemMessage { get; set; }
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
    public float? FrequencyPenalty { get; set; }
    public float? PresencePenalty { get; set; }
    public int? MaxTokens { get; set; }
    public int? PastMessagesCount { get; set; }
    public string[] SelectedA2AConnectionIds { get; set; } = [];

    public List<SelectListItem> Deployments { get; set; } = [];
    public List<A2AConnectionSelectionItem> AvailableA2AConnections { get; set; } = [];
}
