using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Accounts.ViewModels;

/// <summary>
/// The members on an account's editor.
/// </summary>
public class AccountMembersPartViewModel
{
    public List<AccountMemberRowViewModel> Members { get; set; } = [];
}

/// <summary>
/// One member on an account's editor.
/// </summary>
public class AccountMemberRowViewModel
{
    /// <summary>
    /// Gets or sets the member's user name or email.
    /// </summary>
    public string User { get; set; }

    public AccountMemberRole Role { get; set; }

    /// <summary>
    /// Gets or sets the account's contact the member is linked to, or none.
    /// </summary>
    [BindNever]
    public string ContactContentItemId { get; set; }
}

/// <summary>
/// The locations on an account's editor.
/// </summary>
public class AccountLocationsPartViewModel
{
    public List<AccountLocationRowViewModel> Locations { get; set; } = [];
}

/// <summary>
/// One location on an account's editor.
/// </summary>
public class AccountLocationRowViewModel
{
    public string LocationId { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the address as the editor posts it, under the row's <c>Address</c> prefix.
    /// </summary>
    public AddressEditorViewModel Address { get; set; } = new();

    /// <summary>
    /// Gets or sets the address editor, laid out for the address's country.
    /// </summary>
    [BindNever]
    public IShape AddressEditor { get; set; }
}

/// <summary>
/// The Accounts deployment step's editor.
/// </summary>
public class AccountsDeploymentStepViewModel
{
    public bool IncludeRecords { get; set; }
}
