using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Banking.ViewModels;

/// <summary>
/// The page where a person links their bank with a link provider.
/// </summary>
public class BankAccountLinkViewModel
{
    /// <summary>
    /// Gets or sets the provider's name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's display name.
    /// </summary>
    [BindNever]
    public LocalizedString ProviderDisplayName { get; set; }

    /// <summary>
    /// Gets or sets whether linking makes a new account.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the name of the account being linked, when it exists.
    /// </summary>
    public string AccountName { get; set; }

    /// <summary>
    /// Gets or sets the stored number of the account being linked, masked.
    /// </summary>
    public string MaskedNumber { get; set; }

    /// <summary>
    /// Gets or sets, for a new account, the name it is listed by.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets, for a new account, the holder's name as the bank knows it.
    /// </summary>
    public string HolderName { get; set; }

    /// <summary>
    /// Gets or sets the editor of the page's fields, built by display drivers so they are grouped in cards by placement.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }

    /// <summary>
    /// Gets or sets the provider's shape, which runs the browser part.
    /// </summary>
    [BindNever]
    public IShape LinkShape { get; set; }

    /// <summary>
    /// Gets or sets where the form posts.
    /// </summary>
    [BindNever]
    public string FormAction { get; set; }

    /// <summary>
    /// Gets or sets where Cancel goes.
    /// </summary>
    [BindNever]
    public string CancelUrl { get; set; }
}
