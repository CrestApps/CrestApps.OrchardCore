using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditProfileDeploymentViewModel : EditConnectionProfileViewModel
{
    public string DeploymentId { get; set; }

    [BindNever]
    public string ProviderName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; }
}
