using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class SpeechToTextMetadataViewModel
{
    public bool UseMicrophone { get; set; }

    public string ServiceConnectionName { get; set; }

    public string DeploymentId { get; set; }

    [BindNever]
    public string ProviderName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Connections { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; }
}
