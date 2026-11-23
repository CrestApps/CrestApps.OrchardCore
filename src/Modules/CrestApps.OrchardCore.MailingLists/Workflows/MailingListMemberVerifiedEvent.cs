using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Starts when a member of a mailing list proves they own the address.
/// </summary>
public sealed class MailingListMemberVerifiedEvent : MailingListMemberEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMemberVerifiedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MailingListMemberVerifiedEvent(IStringLocalizer<MailingListMemberVerifiedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => nameof(MailingListMemberVerifiedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Mailing List Member Verified"];
}
