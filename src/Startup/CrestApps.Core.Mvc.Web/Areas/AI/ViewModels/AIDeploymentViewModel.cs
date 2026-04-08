using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.AI.ViewModels;

public sealed class AIDeploymentViewModel
{
    public string ItemId { get; set; }

    public string ModelName { get; set; }

    public string TechnicalName { get; set; }
    public string[] SelectedTypes { get; set; } = [];
    public string ConnectionName { get; set; }

    public string ClientName { get; set; }

    // Standalone deployment fields (e.g., Azure AI Services).
    public string Endpoint { get; set; }

    public string AuthenticationType { get; set; }

    public string ApiKey { get; set; }

    public bool IsReadOnly { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Connections { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> Providers { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> AuthenticationTypes { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> Types { get; set; } = [];

    public static AIDeploymentViewModel FromDeployment(AIDeployment deployment)
    {
        var model = new AIDeploymentViewModel
        {
            ItemId = deployment.ItemId,
            ModelName = deployment.ModelName,
            TechnicalName = deployment.Name,
            SelectedTypes = deployment.Type.GetSupportedTypes()
                .Select(static type => type.ToString())
                .ToArray(),
            ConnectionName = deployment.ConnectionName,
            ClientName = AIProviderNameNormalizer.Normalize(deployment.ClientName),
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
        deployment.Name = TechnicalName;
        deployment.ModelName = ModelName;
        deployment.Type = GetDeploymentType();
        deployment.ConnectionName = ConnectionName;
        deployment.ClientName = AIProviderNameNormalizer.Normalize(ClientName);
        deployment.IsDefault = false;

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

    public AIDeploymentType GetDeploymentType()
    {
        var deploymentType = AIDeploymentType.None;

        if (SelectedTypes is null)
        {
            return deploymentType;
        }

        foreach (var typeName in SelectedTypes.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            if (Enum.TryParse<AIDeploymentType>(typeName, ignoreCase: true, out var parsedType)
                && parsedType != AIDeploymentType.None)
            {
                deploymentType |= parsedType;
            }
        }

        return deploymentType;
    }
}
