using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Models;
using CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Handlers;

/// <summary>
/// Records, on every save, whether a data source's self-managed URL went through the save check (review 3, High 2). A
/// new or changed URL is checked by the save, so it is recorded and the client holds it to the same rule when it
/// connects. A URL the save keeps keeps the stored record, or the lack of one: only a data source saved before the
/// check, and not moved since, connects with a private network allowed.
/// </summary>
internal sealed class ElasticsearchCheckedUrlHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly AIDataSourceStoredSettings _storedSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchCheckedUrlHandler"/> class.
    /// </summary>
    /// <param name="storedSettings">The data sources as they were stored, read in this request.</param>
    public ElasticsearchCheckedUrlHandler(AIDataSourceStoredSettings storedSettings = null)
    {
        _storedSettings = storedSettings;
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<AIDataSource> context, CancellationToken cancellationToken = default)
    {
        Record(context.Model, stored: null);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<AIDataSource> context, CancellationToken cancellationToken = default)
    {
        Record(context.Model, _storedSettings?.Find(context.Model.ItemId));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Records whether the data source's self-managed URL was checked by this save.
    /// </summary>
    /// <param name="dataSource">The data source being saved.</param>
    /// <param name="stored">The data source as it was stored, or <see langword="null"/> for a new one.</param>
    internal static void Record(AIDataSource dataSource, AIDataSource stored)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        if (!dataSource.TryGet<ElasticsearchSourceMetadata>(out var metadata))
        {
            return;
        }

        var url = metadata.Url?.Trim();

        // A data connection, an Elastic Cloud ID or the global connection is held to its own rule at connect.
        if (AIDataSourceConnectionPicker.GetConnectionId(dataSource) is not null ||
            string.IsNullOrEmpty(url) ||
            string.Equals(metadata.GetEnvironmentType(), ElasticsearchSourceMetadata.CloudHostedEnvironmentType, StringComparison.OrdinalIgnoreCase))
        {
            dataSource.Remove<ElasticsearchCheckedUrl>();

            return;
        }

        // The save did not check a URL the stored data source used, so that one keeps its stored record, or the lack of it.
        if (stored is not null &&
            stored.TryGet<ElasticsearchSourceMetadata>(out var storedMetadata) &&
            ElasticsearchAIDataSourceSourceHandler.UsedStoredUrl(storedMetadata, AIDataSourceConnectionPicker.GetConnectionId(stored), url) &&
            !stored.Has<ElasticsearchCheckedUrl>())
        {
            dataSource.Remove<ElasticsearchCheckedUrl>();

            return;
        }

        dataSource.Put(new ElasticsearchCheckedUrl { Url = url });
    }
}
