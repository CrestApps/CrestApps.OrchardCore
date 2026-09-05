using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Sms.Workspace.ViewModels;

/// <summary>
/// View model for selecting the SMS provider that owns a channel endpoint's number.
/// </summary>
public sealed class SmsEndpointProviderViewModel
{
    /// <summary>
    /// Gets or sets the selected provider's technical name. Empty means the tenant-default provider.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the enabled SMS providers available for selection.
    /// </summary>
    [BindNever]
    public SelectListItem[] Providers { get; set; }
}
