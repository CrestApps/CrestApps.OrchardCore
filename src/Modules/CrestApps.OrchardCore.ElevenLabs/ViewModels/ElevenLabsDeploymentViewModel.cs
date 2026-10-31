using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ElevenLabs.ViewModels;

public class ElevenLabsDeploymentViewModel
{
    public string Endpoint { get; set; }

    public string ApiKey { get; set; }

    [BindNever]
    public bool HasApiKey { get; set; }
}
