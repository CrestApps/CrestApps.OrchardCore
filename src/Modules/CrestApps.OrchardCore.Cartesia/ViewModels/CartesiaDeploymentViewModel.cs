using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Cartesia.ViewModels;

public class CartesiaDeploymentViewModel
{
    public string Endpoint { get; set; }

    public string ApiKey { get; set; }

    [BindNever]
    public bool HasApiKey { get; set; }
}
