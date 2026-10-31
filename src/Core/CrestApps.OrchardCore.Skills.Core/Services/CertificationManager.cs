using CrestApps.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// The default <see cref="ICertificationManager"/>.
/// </summary>
internal sealed class CertificationManager : CatalogManager<Certification>, ICertificationManager
{
    private readonly CertificationStore _store;

    public CertificationManager(
        CertificationStore store,
        IEnumerable<ICatalogEntryHandler<Certification>> handlers,
        ILogger<CatalogManager<Certification>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<Certification> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var certification = await _store.FindByNameAsync(name, cancellationToken);

        if (certification is not null)
        {
            await LoadAsync(certification, cancellationToken);
        }

        return certification;
    }

    public async Task<IReadOnlyList<Certification>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var certifications = await _store.GetEnabledAsync(cancellationToken);

        foreach (var certification in certifications)
        {
            await LoadAsync(certification, cancellationToken);
        }

        return certifications;
    }

    public async Task<(int Count, IReadOnlyList<Certification> Items)> SearchAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var page = await _store.SearchAsync(search, skip, take, cancellationToken);

        foreach (var certification in page.Items)
        {
            await LoadAsync(certification, cancellationToken);
        }

        return page;
    }
}
