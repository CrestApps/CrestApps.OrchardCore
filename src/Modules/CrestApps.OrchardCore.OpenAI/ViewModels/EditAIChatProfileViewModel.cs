using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class EditAIChatProfileViewModel
{
    public string Name { get; set; }

    public string WelcomeMessage { get; set; }

    public string DeploymentId { get; set; }

    [BindNever]
    public IList<SelectListItem> Deployments { get; set; }

    [BindNever]
    public bool IsNew { get; set; }
}
