namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// Where the templated email outbox keeps its emails.
/// </summary>
public static class TemplatedEmailOutboxStorage
{
    /// <summary>
    /// The YesSql collection the outbox's emails and their index live in.
    /// </summary>
    public const string CollectionName = "TemplatedEmails";
}
