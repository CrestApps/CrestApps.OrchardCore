using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class EditChatInteractionConnectionViewModel
{
    public string ChatDeploymentId { get; set; }

    public string UtilityDeploymentId { get; set; }

    [BindNever]
    public bool ShowMissingDefaultChatDeploymentWarning { get; set; }

    [BindNever]
    public bool ShowMissingDefaultUtilityDeploymentWarning { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ChatDeployments { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> UtilityDeployments { get; set; }
}
