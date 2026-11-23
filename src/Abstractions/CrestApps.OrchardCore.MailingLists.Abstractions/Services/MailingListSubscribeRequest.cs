using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Services;

/// <summary>
/// A request to sign an address up to a mailing list.
/// </summary>
public sealed class MailingListSubscribeRequest
{
    /// <summary>
    /// Gets the id of the list.
    /// </summary>
    public string ListId { get; init; }

    /// <summary>
    /// Gets the e-mail address as the person typed it.
    /// </summary>
    public string Email { get; init; }

    /// <summary>
    /// Gets the person's name, when the form asked for it.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets how the person signed up (see <c>MailingListMemberSources</c>).
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets the id of the thing the person signed up through, such as the widget's content item id.
    /// </summary>
    public string SourceId { get; init; }

    /// <summary>
    /// Gets the id of the signed-in user, when there is one.
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Gets whether the signed-in user's account e-mail is confirmed and is the address signing up.
    /// </summary>
    public bool EmailVerifiedByAccount { get; init; }

    /// <summary>
    /// Gets the address the request came from, kept as consent evidence.
    /// </summary>
    public string ClientAddress { get; init; }

    /// <summary>
    /// Gets where the person agreed, kept as consent evidence (a page URL or a form id).
    /// </summary>
    public string ConsentForm { get; init; }

    /// <summary>
    /// Gets whether to skip every e-mail and the welcome coupon (imports and admin additions).
    /// </summary>
    public bool Silent { get; init; }

    /// <summary>
    /// Gets the state to give the member without any confirmation (imports and admin additions); <see langword="null"/>
    /// follows the list's confirmation setting.
    /// </summary>
    public MailingListMemberStatus? Status { get; init; }
}
