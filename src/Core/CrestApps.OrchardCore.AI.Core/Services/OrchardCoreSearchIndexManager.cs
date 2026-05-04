using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

internal sealed class OrchardCoreSearchIndexManager : ISearchIndexManager
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreSearchIndexManager"/> class.
    /// </summary>
    /// <param name="indexProfileStore">The store for resolving index profiles.</param>
    /// <param name="serviceProvider">The service provider for resolving keyed index managers.</param>
    public OrchardCoreSearchIndexManager(
        IIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider)
    {
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Composes the full index name for the given profile by delegating to the provider-specific
    /// <see cref="IIndexNameProvider"/>, falling back to <see cref="IIndexProfileInfo.IndexFullName"/>.
    /// </summary>
    /// <param name="profile">The index profile to compose the full name for.</param>
    /// <returns>The fully qualified index name.</returns>
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

    /// <summary>
    /// Determines whether the search index for the given profile exists.
    /// </summary>
    /// <param name="profile">The index profile identifying the target search index.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the index exists; otherwise <see langword="false"/>.</returns>
    public async Task<bool> ExistsAsync(IIndexProfileInfo profile, CancellationToken cancellationToken = default)
    {
        var indexProfile = await ResolveIndexProfileAsync(profile);
        var manager = ResolveIndexManager(indexProfile.ProviderName);

        return await manager.ExistsAsync(indexProfile.IndexFullName);
    }

    /// <summary>
    /// Creates a new search index for the given profile.
    /// </summary>
    /// <param name="profile">The index profile identifying the target search index.</param>
    /// <param name="fields">The field definitions for the index schema.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
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

    /// <summary>
    /// Deletes the search index for the given profile.
    /// </summary>
    /// <param name="profile">The index profile identifying the target search index.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
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
