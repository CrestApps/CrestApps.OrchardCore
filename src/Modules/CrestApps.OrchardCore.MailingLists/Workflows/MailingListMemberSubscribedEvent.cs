using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Starts when a member of a mailing list becomes subscribed.
/// </summary>
public sealed class MailingListMemberSubscribedEvent : MailingListMemberEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMemberSubscribedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MailingListMemberSubscribedEvent(IStringLocalizer<MailingListMemberSubscribedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => nameof(MailingListMemberSubscribedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Mailing List Member Subscribed"];
}
