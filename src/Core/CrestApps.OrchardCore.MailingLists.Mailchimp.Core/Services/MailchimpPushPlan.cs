namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// What one push of a member to Mailchimp sends.
/// </summary>
public sealed class MailchimpPushPlan
{
    /// <summary>
    /// The plan of a push that sends nothing.
    /// </summary>
    public static readonly MailchimpPushPlan Skip = new() { Action = MailchimpPushAction.Skip };

    /// <summary>
    /// Gets what the push does.
    /// </summary>
    public MailchimpPushAction Action { get; init; }

    /// <summary>
    /// Gets the Mailchimp status a new address gets (<c>status_if_new</c>) on an upsert. An address Mailchimp already
    /// has keeps its status unless <see cref="MailchimpPushPlanner.FollowUpStatus"/> says otherwise.
    /// </summary>
    public string StatusIfNew { get; init; }
}
