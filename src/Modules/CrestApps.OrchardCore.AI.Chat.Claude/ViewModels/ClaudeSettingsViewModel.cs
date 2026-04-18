using CrestApps.Core.AI.Claude.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Claude.ViewModels;

public class ClaudeSettingsViewModel
{
    public ClaudeAuthenticationType AuthenticationType { get; set; }

    public string BaseUrl { get; set; }

    public string ApiKey { get; set; }

    public bool HasApiKey { get; set; }

    public string DefaultModel { get; set; }

    [BindNever]
    public IList<SelectListItem> AuthenticationTypes { get; set; }

    [BindNever]
    public IList<SelectListItem> AvailableModels { get; set; }
}
