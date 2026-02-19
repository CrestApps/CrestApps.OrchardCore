using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class EditChatInteractionConnectionViewModel
{
    public string ConnectionName { get; set; }

    public string DeploymentId { get; set; }

    [BindNever]
    public string ProviderName { get; set; }

    [BindNever]
    public IList<SelectListItem> ConnectionNames { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; }
}
