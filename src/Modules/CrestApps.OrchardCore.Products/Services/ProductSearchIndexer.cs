using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
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
    private readonly ILogger _logger;

    public ProductSearchIndexer(
        ISession session,
        ProductSearchDocumentBuilder documentBuilder,
        ProductAttributeCatalog attributeCatalog,
        ILogger<ProductSearchIndexer> logger)
    {
        _session = session;
        _documentBuilder = documentBuilder;
        _attributeCatalog = attributeCatalog;
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

        await RemoveAsync(contentItem.ContentItemId);

        if (document is not null)
        {
            await _session.SaveAsync(document);
        }
    }

    /// <summary>
    /// Removes the record of a product.
    /// </summary>
    /// <param name="contentItemId">The product's content item identifier.</param>
    public async Task RemoveAsync(string contentItemId)
    {
        var existing = await _session
            .Query<ProductSearchDocument, ProductSearchIndex>(index => index.ContentItemId == contentItemId)
            .ListAsync();

        foreach (var document in existing)
        {
            _session.Delete(document);
        }
    }

    /// <summary>
    /// A fingerprint of everything that decides what the records contain. When it changes, the records are
    /// stale and must be rebuilt.
    /// </summary>
    public async Task<string> GetFingerprintAsync()
    {
        var builder = new StringBuilder(BuildVersion);

        foreach (var type in await _attributeCatalog.GetProductTypesAsync())
        {
            builder.Append('|').Append(type.Name).Append(':').Append(type.DisplayName);
            builder.Append(JsonSerializer.Serialize(ProductAttributeCatalog.GetProductSettings(type)));

            foreach (var field in _attributeCatalog.GetFields(type))
            {
                builder.Append(';').Append(field.Part.Name).Append('.').Append(field.Field.Name);
                builder.Append(JsonSerializer.Serialize(field.Field.GetSettings<ProductAttributeSettings>()));
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>
    /// Rebuilds every record. Each batch runs in its own shell scope, so it commits on its own and a large
    /// catalog never holds one transaction or one identity map for the whole run.
    /// </summary>
    /// <param name="shellHost">The shell host.</param>
    /// <param name="shellSettings">The current tenant.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of products indexed.</returns>
    public static async Task<int> RebuildAsync(IShellHost shellHost, ShellSettings shellSettings, CancellationToken cancellationToken)
    {
        // Remove the old records first, in batches, each in its own scope.
        while (!cancellationToken.IsCancellationRequested)
        {
            var removed = 0;

            await (await shellHost.GetScopeAsync(shellSettings)).UsingAsync(async scope =>
            {
                var session = scope.ServiceProvider.GetRequiredService<ISession>();
                var documents = await session.Query<ProductSearchDocument, ProductSearchIndex>()
                    .OrderBy(index => index.Id)
                    .Take(BatchSize)
                    .ListAsync(cancellationToken);

                foreach (var document in documents)
                {
                    session.Delete(document);
                    removed++;
                }
            });

            if (removed < BatchSize)
            {
                break;
            }
        }

        var indexed = 0;
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

                foreach (var item in items)
                {
                    batchCount++;
                    lastId = item.Id;

                    var document = await builder.BuildAsync(item);

                    if (document is not null)
                    {
                        await session.SaveAsync(document, cancellationToken: cancellationToken);
                        indexed++;
                    }
                }
            });

            if (batchCount < BatchSize)
            {
                break;
            }
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
