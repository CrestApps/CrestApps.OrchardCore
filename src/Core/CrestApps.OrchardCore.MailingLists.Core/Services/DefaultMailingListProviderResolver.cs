using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Finds a list's provider among the registered ones. <see cref="Resolve"/> falls back to the Local provider when the
/// list's provider is not registered (its feature is off), so sign-ups keep working and the list shows a warning; the
/// provider outbox finds the provider by name instead and keeps the list's changes until the provider is back.
/// </summary>
public sealed class DefaultMailingListProviderResolver : IMailingListProviderResolver
{
    private readonly IReadOnlyList<IMailingListProvider> _providers;
    private readonly IMailingListProvider _local;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMailingListProviderResolver"/> class.
    /// </summary>
    /// <param name="providers">The registered providers.</param>
    public DefaultMailingListProviderResolver(IEnumerable<IMailingListProvider> providers)
    {
        var all = providers
            .Where(provider => !string.IsNullOrEmpty(provider?.Name))
            .DistinctBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _local = all.FirstOrDefault(provider => string.Equals(provider.Name, MailingListsConstants.LocalProviderName, StringComparison.OrdinalIgnoreCase));
        _providers = [.. all
            .OrderBy(provider => ReferenceEquals(provider, _local) ? 0 : 1)
            .ThenBy(provider => provider.DisplayName?.Value ?? provider.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<IMailingListProvider> GetAll()
        => _providers;

    /// <inheritdoc/>
    public IMailingListProvider Find(string name)
        => string.IsNullOrWhiteSpace(name)
            ? null
            : _providers.FirstOrDefault(provider => string.Equals(provider.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc/>
    public IMailingListProvider Resolve(MailingList list)
        => Find(list?.ProviderName) ?? _local;
}
