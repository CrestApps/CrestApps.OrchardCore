using CrestApps.OrchardCore.Banking.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Banking.ViewModels;

/// <summary>
/// The bank account editor.
/// </summary>
public class BankAccountViewModel
{
    /// <summary>
    /// Gets or sets the name the account is listed by.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the owner as <c>Kind:Id</c>; empty for one of the business's own accounts.
    /// </summary>
    public string Owner { get; set; }

    /// <summary>
    /// Gets or sets the account holder's name.
    /// </summary>
    public string HolderName { get; set; }

    /// <summary>
    /// Gets or sets the kind of account, such as checking, savings or a credit card.
    /// </summary>
    public BankAccountKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the two-letter country code of the bank.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets the three-letter currency code.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the bank's name.
    /// </summary>
    public string BankName { get; set; }

    /// <summary>
    /// Gets or sets the routing number.
    /// </summary>
    public string RoutingNumber { get; set; }

    /// <summary>
    /// Gets or sets a new account number; blank keeps the stored one. It is never sent back to the browser.
    /// </summary>
    public string AccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the last digits of an account number the site doesn't hold, such as for an imported account.
    /// </summary>
    public string AccountNumberLastFour { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public BankAccountStatus Status { get; set; }

    /// <summary>
    /// Gets or sets why the status was changed.
    /// </summary>
    public string StatusReason { get; set; }

    /// <summary>
    /// Gets or sets the stored account number, masked.
    /// </summary>
    [BindNever]
    public string MaskedAccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the selected owner for the owner picker, as a JSON array of <c>{ value, text }</c>.
    /// </summary>
    [BindNever]
    public string OwnerInitialItemsJson { get; set; }

    /// <summary>
    /// Gets or sets the owners a person may choose from on their own accounts page.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> OwnOwnerOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses the account may move to.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the account kinds.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> KindOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the account is being added.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets whether a person is editing their own account, which hides the owner and the status.
    /// </summary>
    [BindNever]
    public bool IsOwnAccount { get; set; }
}
