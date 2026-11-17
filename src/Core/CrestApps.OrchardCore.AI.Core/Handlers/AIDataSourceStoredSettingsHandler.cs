using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Keeps a copy of each data source the catalog reads, as it was stored, in <see cref="AIDataSourceStoredSettings"/>.
/// </summary>
internal sealed class AIDataSourceStoredSettingsHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly AIDataSourceStoredSettings _storedSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceStoredSettingsHandler"/> class.
    /// </summary>
    /// <param name="storedSettings">The copies of the data sources read in this request.</param>
    public AIDataSourceStoredSettingsHandler(AIDataSourceStoredSettings storedSettings)
    {
        _storedSettings = storedSettings;
    }

    /// <inheritdoc/>
    public override Task LoadedAsync(LoadedContext<AIDataSource> context, CancellationToken cancellationToken = default)
    {
        _storedSettings.Remember(context.Model);

        return Task.CompletedTask;
    }
}
