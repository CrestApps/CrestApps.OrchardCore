using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

internal sealed class OrchardCoreSearchIndexManager : ISearchIndexManager
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IServiceProvider _serviceProvider;

    public OrchardCoreSearchIndexManager(
        IIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider)
    {
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;
    }

    public string ComposeIndexFullName(IIndexProfileInfo profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!string.IsNullOrWhiteSpace(profile.ProviderName) &&
            !string.IsNullOrWhiteSpace(profile.IndexName))
        {
            var nameProvider = _serviceProvider.GetKeyedService<IIndexNameProvider>(profile.ProviderName);

            if (nameProvider != null)
            {
                return nameProvider.GetFullIndexName(profile.IndexName);
            }
        }

        return profile.IndexFullName;
    }

    public async Task<bool> ExistsAsync(IIndexProfileInfo profile, CancellationToken cancellationToken = default)
    {
        var indexProfile = await ResolveIndexProfileAsync(profile);
        var manager = ResolveIndexManager(indexProfile.ProviderName);

        return await manager.ExistsAsync(indexProfile.IndexFullName);
    }

    public async Task CreateAsync(
        IIndexProfileInfo profile,
        IReadOnlyCollection<SearchIndexField> fields,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var indexProfile = await ResolveIndexProfileAsync(profile);
        var manager = ResolveIndexManager(indexProfile.ProviderName);

        await manager.CreateAsync(indexProfile);
    }

    public async Task DeleteAsync(IIndexProfileInfo profile, CancellationToken cancellationToken = default)
    {
        var indexProfile = await ResolveIndexProfileAsync(profile);
        var manager = ResolveIndexManager(indexProfile.ProviderName);

        await manager.DeleteAsync(indexProfile);
    }

    private async ValueTask<IndexProfile> ResolveIndexProfileAsync(IIndexProfileInfo profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        IndexProfile indexProfile = null;

        if (!string.IsNullOrWhiteSpace(profile.IndexProfileId))
        {
            indexProfile = await _indexProfileStore.FindByIdAsync(profile.IndexProfileId);
        }

        if (indexProfile == null &&
            !string.IsNullOrWhiteSpace(profile.IndexName) &&
            !string.IsNullOrWhiteSpace(profile.ProviderName))
        {
            indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(profile.IndexName, profile.ProviderName);
        }

        if (indexProfile == null)
        {
            throw new InvalidOperationException(
                $"The Orchard Core index profile '{profile.IndexProfileId ?? profile.IndexName}' for provider '{profile.ProviderName}' could not be found.");
        }

        return indexProfile;
    }

    private IIndexManager ResolveIndexManager(string providerName)
        => _serviceProvider.GetRequiredKeyedService<IIndexManager>(providerName);
}
