using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class OpenAIConnectionViewModel
{
    public string Endpoint { get; set; }

    public string ApiKey { get; set; }

    [BindNever]
    public bool HasApiKey { get; set; }
}
