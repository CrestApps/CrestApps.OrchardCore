using CrestApps.OrchardCore.MailingLists.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.MailingLists.ViewModels;

/// <summary>
/// The form that adds a member to a list by hand.
/// </summary>
public class AddMailingListMemberViewModel
{
    /// <summary>
    /// Gets or sets the mailing list.
    /// </summary>
    [BindNever]
    public MailingList MailingList { get; set; }

    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the person's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the admin confirms they hold the person's consent to receive the list.
    /// </summary>
    public bool ConsentConfirmed { get; set; }

    /// <summary>
    /// Gets or sets where the consent was given, kept as evidence, such as "Signed up at the trade fair".
    /// </summary>
    public string ConsentNote { get; set; }
}
