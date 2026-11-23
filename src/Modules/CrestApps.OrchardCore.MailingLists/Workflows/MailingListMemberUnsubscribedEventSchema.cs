namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Describes the Mailing List Member Unsubscribed event.
/// </summary>
internal sealed class MailingListMemberUnsubscribedEventSchema : MailingListMemberEventSchemaBase
{
    public override string Name => "MailingListMemberUnsubscribedEvent";

    protected override string DisplayText => "Mailing List Member Unsubscribed";

    protected override string Description => "Starts when a member of a mailing list is unsubscribed or cleaned";
}
