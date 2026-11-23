namespace CrestApps.OrchardCore.MailingLists.Core.Sync;

/// <summary>
/// What one batch of the provider outbox did.
/// </summary>
/// <param name="Claimed">How many changes were claimed.</param>
/// <param name="Sent">How many changes the providers took.</param>
/// <param name="Retried">How many changes wait for another try.</param>
/// <param name="Abandoned">How many changes were given up; their members show "not synced".</param>
/// <param name="Waiting">How many changes wait for their provider to be turned on or set up, without using up an attempt.</param>
public readonly record struct MailingListSyncBatchResult(int Claimed, int Sent, int Retried, int Abandoned, int Waiting);
