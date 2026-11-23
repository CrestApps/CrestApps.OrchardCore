using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Starts when a member of a mailing list is unsubscribed or cleaned.
/// </summary>
public sealed class MailingListMemberUnsubscribedEvent : MailingListMemberEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMemberUnsubscribedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MailingListMemberUnsubscribedEvent(IStringLocalizer<MailingListMemberUnsubscribedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => nameof(MailingListMemberUnsubscribedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Mailing List Member Unsubscribed"];
}
