using CrestApps.OrchardCore.MailingLists.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.MailingLists.Services;

/// <summary>
/// Where a mailing list lives. The site always keeps its member records; a provider other than the built-in Local one
/// receives every member change through the durable provider outbox, and reports its own changes back. Register one with
/// <c>services.AddMailingListProvider&lt;T&gt;()</c>.
/// </summary>
public interface IMailingListProvider
{
    /// <summary>
    /// Gets the provider's permanent technical name, stored on each list.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name the admin shows.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets what the provider can do.
    /// </summary>
    MailingListProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Returns whether the provider has the settings it needs (for example an API key).
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends one member change to the provider. It must be safe to call twice for the same change: the outbox retries.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="member">The member as it is now on the site.</param>
    /// <param name="kind">What to do.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>Whether the provider took the change, and the member's id there.</returns>
    Task<MailingListPushResult> PushAsync(MailingList list, MailingListMember member, MailingListPushKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the members that changed at the provider since a time, for the daily reconciliation. Providers without
    /// <see cref="MailingListProviderCapabilities.ReportsChanges"/> return an empty page.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="sinceUtc">The time of the last reconciliation (UTC).</param>
    /// <param name="cursor">The cursor of the next page, or <see langword="null"/> for the first.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A page of changes and the cursor of the next page.</returns>
    Task<MailingListProviderChangePage> GetChangesAsync(MailingList list, DateTime sinceUtc, string cursor, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a mailing list provider can do.
/// </summary>
[Flags]
public enum MailingListProviderCapabilities
{
    /// <summary>
    /// Nothing beyond keeping members.
    /// </summary>
    None = 0,

    /// <summary>
    /// Sends its own confirmation e-mail (double opt-in) and reports the confirmation.
    /// </summary>
    ConfirmsAddresses = 1,

    /// <summary>
    /// Can hold the welcome coupon code on the member's profile, for the provider's own welcome e-mail.
    /// </summary>
    DeliversWelcomeOffer = 2,

    /// <summary>
    /// Reports its members' changes (webhooks and <see cref="IMailingListProvider.GetChangesAsync"/>).
    /// </summary>
    ReportsChanges = 4,

    /// <summary>
    /// Sends campaigns to the list from its own tools.
    /// </summary>
    SendsCampaigns = 8,
}

/// <summary>
/// What a push asks the provider to do.
/// </summary>
public enum MailingListPushKind
{
    /// <summary>
    /// Create or update the member with its current state and profile.
    /// </summary>
    Upsert,

    /// <summary>
    /// Mark the member unsubscribed.
    /// </summary>
    Unsubscribe,

    /// <summary>
    /// Erase every trace of the member (a privacy request).
    /// </summary>
    Erase,
}

/// <summary>
/// The outcome of <see cref="IMailingListProvider.PushAsync"/>.
/// </summary>
public sealed class MailingListPushResult
{
    /// <summary>
    /// Gets whether the provider took the change.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets whether retrying can help (a time-out or a rate limit), as opposed to a refusal.
    /// </summary>
    public bool CanRetry { get; init; }

    /// <summary>
    /// Gets the member's id at the provider.
    /// </summary>
    public string ProviderMemberId { get; init; }

    /// <summary>
    /// Gets the provider's error, when the change was not taken.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Returns a successful result.
    /// </summary>
    /// <param name="providerMemberId">The member's id at the provider.</param>
    public static MailingListPushResult Success(string providerMemberId = null)
        => new() { Succeeded = true, ProviderMemberId = providerMemberId };

    /// <summary>
    /// Returns a failed result.
    /// </summary>
    /// <param name="error">The provider's error.</param>
    /// <param name="canRetry">Whether retrying can help.</param>
    public static MailingListPushResult Failure(string error, bool canRetry)
        => new() { Error = error, CanRetry = canRetry };
}

/// <summary>
/// A page of changes read from a provider.
/// </summary>
public sealed class MailingListProviderChangePage
{
    /// <summary>
    /// An empty, last page.
    /// </summary>
    public static readonly MailingListProviderChangePage Empty = new();

    /// <summary>
    /// Gets the changes.
    /// </summary>
    public IReadOnlyList<MailingListProviderChange> Changes { get; init; } = [];

    /// <summary>
    /// Gets the cursor of the next page, or <see langword="null"/> on the last page.
    /// </summary>
    public string NextCursor { get; init; }
}
