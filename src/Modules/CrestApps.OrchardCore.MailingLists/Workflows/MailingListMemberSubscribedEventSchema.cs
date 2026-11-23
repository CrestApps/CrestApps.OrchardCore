namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Describes the Mailing List Member Subscribed event.
/// </summary>
internal sealed class MailingListMemberSubscribedEventSchema : MailingListMemberEventSchemaBase
{
    public override string Name => "MailingListMemberSubscribedEvent";

    protected override string DisplayText => "Mailing List Member Subscribed";

    protected override string Description => "Starts when a member of a mailing list becomes subscribed";
}
