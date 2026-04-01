using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditProfileDeploymentViewModel
{
    public string ChatDeploymentName { get; set; }

    public string UtilityDeploymentName { get; set; }

    [BindNever]
    public bool ShowMissingDefaultChatDeploymentWarning { get; set; }

    [BindNever]
    public bool ShowMissingDefaultUtilityDeploymentWarning { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> ChatDeployments { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> UtilityDeployments { get; set; }
}
