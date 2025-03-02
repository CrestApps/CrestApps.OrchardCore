using CrestApps.OrchardCore.AzureAIInference.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AzureAIInference.ViewModels;

public class AzureAIInferenceConnectionViewModel
{
    public AzureAuthenticationType AuthenticationType { get; set; }

    public string ApiKey { get; set; }

    [BindNever]
    public bool HasApiKey { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> AuthenticationTypes { get; set; }
}
