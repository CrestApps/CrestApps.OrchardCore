namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Describes the Mailing List Member Verified event.
/// </summary>
internal sealed class MailingListMemberVerifiedEventSchema : MailingListMemberEventSchemaBase
{
    public override string Name => "MailingListMemberVerifiedEvent";

    protected override string DisplayText => "Mailing List Member Verified";

    protected override string Description => "Starts when a member of a mailing list proves they own the address";
}
