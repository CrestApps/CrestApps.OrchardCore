using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Search;
using CrestApps.OrchardCore.Products.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Environment.Cache;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Keeps the product search records in step with published products.
/// </summary>
public sealed class ProductSearchIndexer
{
    private const int BatchSize = 100;

    // Bump when the record's shape or the way it is built changes, so every tenant rebuilds once.
    private const string BuildVersion = "1";

    private readonly ISession _session;
    private readonly ProductSearchDocumentBuilder _documentBuilder;
    private readonly ProductAttributeCatalog _attributeCatalog;
    private readonly Attributes.TaxonomyTermLookup _termLookup;
    private readonly ISignal _signal;
    private readonly IEnumerable<IProductSearchIndexHandler> _handlers;
    private readonly IEnumerable<IProductSearchDocumentContributor> _contributors;
    private readonly ILogger _logger;

    public ProductSearchIndexer(
        ISession session,
        ProductSearchDocumentBuilder documentBuilder,
        ProductAttributeCatalog attributeCatalog,
        Attributes.TaxonomyTermLookup termLookup,
        ISignal signal,
        IEnumerable<IProductSearchIndexHandler> handlers,
        IEnumerable<IProductSearchDocumentContributor> contributors,
        ILogger<ProductSearchIndexer> logger)
    {
        _session = session;
        _documentBuilder = documentBuilder;
        _attributeCatalog = attributeCatalog;
        _termLookup = termLookup;
        _signal = signal;
        _handlers = handlers;
        _contributors = contributors;
        _logger = logger;
    }

    /// <summary>
    /// Creates or replaces the record of a published product, or removes it when the item is not a product.
    /// </summary>
    /// <param name="contentItem">The published content item.</param>
    public async Task IndexAsync(ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        var document = await _documentBuilder.BuildAsync(contentItem);

        await DeleteRecordsAsync(contentItem.ContentItemId);

        if (document is null)
        {
            await _handlers.InvokeAsync((handler, ids) => handler.RemovedAsync(ids), (IReadOnlyCollection<string>)[contentItem.ContentItemId], _logger);

            return;
        }

        await _session.SaveAsync(document);

        // Search servers get the same record. A failing one is logged; the database record still serves listings.
        await _handlers.InvokeAsync((handler, documents) => handler.IndexedAsync(documents), (IReadOnlyCollection<ProductSearchDocument>)[document], _logger);
    }

    /// <summary>
    /// Removes the record of a product.
    /// </summary>
    /// <param name="contentItemId">The product's content item identifier.</param>
    public async Task RemoveAsync(string contentItemId)
    {
        await DeleteRecordsAsync(contentItemId);
        await _handlers.InvokeAsync((handler, ids) => handler.RemovedAsync(ids), (IReadOnlyCollection<string>)[contentItemId], _logger);
    }

    private async Task DeleteRecordsAsync(string contentItemId)
    {
        var existing = await _session
            .Query<ProductSearchDocument, ProductSearchIndex>(index => index.ContentItemId == contentItemId)
            .ListAsync();

        foreach (var document in existing)
        {
            _session.Delete(document);
        }

        _signal.DeferredSignalToken(CachedProductSearchService.SignalKey);
    }

    /// <summary>
    /// A fingerprint of everything that decides what the records contain. When it changes, the records are
    /// stale and must be rebuilt.
    /// </summary>
    public async Task<string> GetFingerprintAsync()
    {
        var builder = new StringBuilder(BuildVersion);

        // A module that adds to the records, turned on or off, changes what every record holds.
        foreach (var contributor in _contributors.Select(contributor => contributor.GetType().FullName).Order(StringComparer.Ordinal))
        {
            builder.Append("|contributor:").Append(contributor);
        }

        foreach (var type in await _attributeCatalog.GetProductTypesAsync())
        {
            builder.Append('|').Append(type.Name).Append(':').Append(type.DisplayName);
            builder.Append(JsonSerializer.Serialize(ProductAttributeCatalog.GetProductSettings(type)));
            builder.Append(JsonSerializer.Serialize(ProductAttributeCatalog.GetVariantOptions(type)));

            foreach (var field in _attributeCatalog.GetFields(type))
            {
                builder.Append(';').Append(field.Part.Name).Append('.').Append(field.Field.Name);
                builder.Append(JsonSerializer.Serialize(field.Field.GetSettings<ProductAttributeSettings>()));
            }
        }

        // Records hold term names and slugs. Orchard's term editor saves a taxonomy that is not draftable without
        // raising a content event, so a rename is caught here instead.
        var taxonomyIds = (await _attributeCatalog.GetAttributesAsync([]))
            .Select(attribute => attribute.TaxonomyContentItemId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        foreach (var taxonomyId in taxonomyIds)
        {
            builder.Append("|taxonomy:").Append(taxonomyId);

            var terms = await _termLookup.GetTermsAsync(taxonomyId);

            if (terms is null)
            {
                continue;
            }

            foreach (var term in terms.Values.OrderBy(term => term.Order))
            {
                builder.Append(';').Append(term.ContentItemId).Append('=').Append(term.DisplayText).Append('<').Append(term.ParentId);
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>
    /// Rebuilds every record. Each batch runs in its own shell scope, so it commits on its own and a large
    /// catalog never holds one transaction or one identity map for the whole run. Records are replaced product by
    /// product, and only the ones left over are removed at the end, so a listing never shows empty while the
    /// rebuild runs.
    /// </summary>
    /// <param name="shellHost">The shell host.</param>
    /// <param name="shellSettings">The current tenant.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of products indexed.</returns>
    public static async Task<int> RebuildAsync(IShellHost shellHost, ShellSettings shellSettings, CancellationToken cancellationToken)
    {
        var indexed = 0;
        var current = new HashSet<string>(StringComparer.Ordinal);
        var lastId = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batchCount = 0;

            await (await shellHost.GetScopeAsync(shellSettings)).UsingAsync(async scope =>
            {
                var services = scope.ServiceProvider;
                var session = services.GetRequiredService<ISession>();
                var catalog = services.GetRequiredService<ProductAttributeCatalog>();
                var builder = services.GetRequiredService<ProductSearchDocumentBuilder>();

                var types = (await catalog.GetProductTypesAsync()).Select(type => type.Name).ToArray();

                if (types.Length == 0)
                {
                    return;
                }

                var items = await session.Query<ContentItem, ContentItemIndex>(index =>
                        index.Published && index.ContentType.IsIn(types) && index.DocumentId > lastId)
                    .OrderBy(index => index.DocumentId)
                    .Take(BatchSize)
                    .ListAsync(cancellationToken);

                var ids = items.Select(item => item.ContentItemId).ToArray();
                var existing = ids.Length == 0
                    ? []
                    : await session.Query<ProductSearchDocument, ProductSearchIndex>(index => index.ContentItemId.IsIn(ids)).ListAsync(cancellationToken);

                foreach (var document in existing)
                {
                    session.Delete(document);
                }

                services.GetRequiredService<ISignal>().DeferredSignalToken(CachedProductSearchService.SignalKey);

                foreach (var item in items)
                {
                    batchCount++;
                    lastId = item.Id;

                    var document = await builder.BuildAsync(item);

                    if (document is not null)
                    {
                        await session.SaveAsync(document, cancellationToken: cancellationToken);
                        current.Add(item.ContentItemId);
                        indexed++;
                    }
                }
            });

            if (batchCount < BatchSize)
            {
                break;
            }
        }

        // Remove the records of products that are no longer published or no longer products. Each batch commits
        // its removals, so the next batch skips only the records that were kept.
        var kept = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batchCount = 0;

            await (await shellHost.GetScopeAsync(shellSettings)).UsingAsync(async scope =>
            {
                var session = scope.ServiceProvider.GetRequiredService<ISession>();
                var documents = await session.Query<ProductSearchDocument, ProductSearchIndex>()
                    .OrderBy(index => index.Id)
                    .Skip(kept)
                    .Take(BatchSize)
                    .ListAsync(cancellationToken);

                foreach (var document in documents)
                {
                    batchCount++;

                    if (current.Contains(document.ContentItemId))
                    {
                        kept++;
                    }
                    else
                    {
                        session.Delete(document);
                    }
                }

                scope.ServiceProvider.GetRequiredService<ISignal>().DeferredSignalToken(CachedProductSearchService.SignalKey);
            });

            if (batchCount < BatchSize)
            {
                break;
            }
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            // The records are committed, so search servers are written again from them.
            await (await shellHost.GetScopeAsync(shellSettings)).UsingAsync(async scope =>
            {
                var handlers = scope.ServiceProvider.GetServices<IProductSearchIndexHandler>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProductSearchIndexer>>();

                await handlers.InvokeAsync((handler, token) => handler.RebuiltAsync(token), cancellationToken, logger);
            });
        }

        return indexed;
    }

    /// <summary>
    /// Records the fingerprint the records were built with.
    /// </summary>
    /// <param name="fingerprint">The fingerprint.</param>
    public async Task SaveStateAsync(string fingerprint)
    {
        var state = await _session.Query<ProductSearchIndexState>().FirstOrDefaultAsync() ?? new ProductSearchIndexState();

        state.Fingerprint = fingerprint;
        state.BuiltUtc = DateTime.UtcNow;

        await _session.SaveAsync(state);
    }

    /// <summary>
    /// Gets the fingerprint the records were last built with.
    /// </summary>
    public async Task<string> GetSavedFingerprintAsync()
        => (await _session.Query<ProductSearchIndexState>().FirstOrDefaultAsync())?.Fingerprint;

    /// <summary>
    /// Forgets the saved fingerprint, so the next indexing pass rebuilds every record.
    /// </summary>
    public async Task RequestRebuildAsync()
    {
        var state = await _session.Query<ProductSearchIndexState>().FirstOrDefaultAsync();

        if (state is not null)
        {
            state.Fingerprint = null;
            await _session.SaveAsync(state);
        }

        _logger.LogInformation("A rebuild of the product search index was requested.");
    }
}

/// <summary>
/// Records what the product search records were last built from.
/// </summary>
public sealed class ProductSearchIndexState
{
    public long Id { get; set; }

    public string Fingerprint { get; set; }

    public DateTime BuiltUtc { get; set; }
}
