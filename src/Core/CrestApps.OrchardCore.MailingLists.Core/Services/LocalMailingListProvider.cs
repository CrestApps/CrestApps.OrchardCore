using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// The built-in provider that keeps a list on the site: its members are already the site's records, so a push does
/// nothing and there are no outside changes to read.
/// </summary>
public sealed class LocalMailingListProvider : IMailingListProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalMailingListProvider"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public LocalMailingListProvider(IStringLocalizer<LocalMailingListProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name
        => MailingListsConstants.LocalProviderName;

    /// <inheritdoc/>
    public LocalizedString DisplayName
        => S["This site"];

    /// <inheritdoc/>
    public MailingListProviderCapabilities Capabilities
        => MailingListProviderCapabilities.None;

    /// <inheritdoc/>
    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc/>
    public Task<MailingListPushResult> PushAsync(MailingList list, MailingListMember member, MailingListPushKind kind, CancellationToken cancellationToken = default)
        => Task.FromResult(MailingListPushResult.Success(member?.ProviderMemberId));

    /// <inheritdoc/>
    public Task<MailingListProviderChangePage> GetChangesAsync(MailingList list, DateTime sinceUtc, string cursor, CancellationToken cancellationToken = default)
        => Task.FromResult(MailingListProviderChangePage.Empty);
}
