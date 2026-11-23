using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.MailingLists.ViewModels;

/// <summary>
/// The Provider card of the mailing list editor.
/// </summary>
public class MailingListProviderViewModel
{
    /// <summary>
    /// Gets or sets the name of the provider that keeps the list.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the registered providers.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Providers { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the list's provider is not registered (its feature is off), so the list's member changes wait
    /// in the provider outbox until it is back.
    /// </summary>
    [BindNever]
    public bool ProviderMissing { get; set; }

    /// <summary>
    /// Gets or sets whether the list's provider lacks the settings it needs.
    /// </summary>
    [BindNever]
    public bool ProviderNotConfigured { get; set; }
}
