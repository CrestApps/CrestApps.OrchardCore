using CrestApps.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.ViewModels;

public sealed class AIDeploymentViewModel
{
    public string ItemId { get; set; }
    public string Name { get; set; }
    public AIDeploymentType Type { get; set; }
    public string ConnectionName { get; set; }
    public string ClientName { get; set; }
    public bool IsDefault { get; set; }

    // Standalone deployment fields (e.g., Azure AI Services).
    public string Endpoint { get; set; }
    public string AuthenticationType { get; set; }
    public string ApiKey { get; set; }

    public List<SelectListItem> Connections { get; set; } = [];
    public List<SelectListItem> Providers { get; set; } = [];
    public List<SelectListItem> AuthenticationTypes { get; set; } = [];

    public static AIDeploymentViewModel FromDeployment(AIDeployment deployment)
    {
        var model = new AIDeploymentViewModel
        {
            ItemId = deployment.ItemId,
            Name = deployment.Name,
            Type = deployment.Type,
            ConnectionName = deployment.ConnectionName,
            ClientName = deployment.ClientName,
            IsDefault = deployment.IsDefault,
        };

        if (deployment.Properties != null)
        {
            model.Endpoint = deployment.Properties.TryGetValue("Endpoint", out var ep) ? ep?.ToString() : null;
            model.ApiKey = deployment.Properties.TryGetValue("ApiKey", out var key) ? key?.ToString() : null;
            model.AuthenticationType = deployment.Properties.TryGetValue("AuthenticationType", out var auth) ? auth?.ToString() : null;
        }

        return model;
    }

    public void ApplyTo(AIDeployment deployment)
    {
        deployment.Name = Name;
        deployment.Type = Type;
        deployment.ConnectionName = ConnectionName;
        deployment.ClientName = ClientName;
        deployment.IsDefault = IsDefault;

        deployment.Properties ??= new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(Endpoint))
        {
            deployment.Properties["Endpoint"] = Endpoint;
        }
        else
        {
            deployment.Properties.Remove("Endpoint");
        }

        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            deployment.Properties["ApiKey"] = ApiKey;
        }

        if (!string.IsNullOrWhiteSpace(AuthenticationType))
        {
            deployment.Properties["AuthenticationType"] = AuthenticationType;
        }
        else
        {
            deployment.Properties.Remove("AuthenticationType");
        }
    }
}
