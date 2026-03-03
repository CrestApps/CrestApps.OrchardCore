using CrestApps.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.ViewModels;

public sealed class AIDeploymentViewModel
{
    public string ItemId { get; set; }
    public string Name { get; set; }
    public string ConnectionName { get; set; }
    public string ProviderName { get; set; }

    public List<SelectListItem> Connections { get; set; } = [];
    public List<SelectListItem> Providers { get; set; } = [];

    public static AIDeploymentViewModel FromDeployment(AIDeployment deployment)
    {
        return new AIDeploymentViewModel
        {
            ItemId = deployment.ItemId,
            Name = deployment.Name,
            ConnectionName = deployment.ConnectionName,
            ProviderName = deployment.ProviderName,
        };
    }

    public void ApplyTo(AIDeployment deployment)
    {
        deployment.Name = Name;
        deployment.ConnectionName = ConnectionName;
        deployment.ProviderName = ProviderName;
    }
}
