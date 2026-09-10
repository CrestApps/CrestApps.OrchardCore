using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The menu provider for a tenant with no telephony provider that can play one. It plays nothing and says so, so
/// an entry point with a menu configured falls through to its ordinary routing instead of throwing at somebody
/// who is already on the line.
/// </summary>
public sealed class NoIvrProvider : IIvrProvider
{
    /// <inheritdoc/>
    public Task<bool> PromptAsync(
        string providerCallId,
        string text,
        string mediaId,
        string validDigits,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
