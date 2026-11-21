using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core.Extensions;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

/// <summary>
/// Validates a data source with its source's own handler, so every save path that validates through the catalog, such
/// as the API and the recipe step, applies the source's checks: a search endpoint on the site's own network, a missing
/// index name or API key.
/// </summary>
/// <remarks>
/// The package's catalog handler checks only the fields every data source has; each source handler is registered keyed
/// by its source type, and nothing in the catalog called its <see cref="IAIDataSourceSourceHandler.ValidateAsync"/>.
/// </remarks>
internal sealed class AIDataSourceSourceValidationHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly IEnumerable<IAIDataSourceSourceHandler> _sourceHandlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceSourceValidationHandler"/> class.
    /// </summary>
    /// <param name="sourceHandlers">Every source handler, registered with <c>AddAIDataSourceSourceHandler</c>.</param>
    public AIDataSourceSourceValidationHandler(
        IEnumerable<IAIDataSourceSourceHandler> sourceHandlers)
    {
        _sourceHandlers = sourceHandlers;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<AIDataSource> context, CancellationToken cancellationToken = default)
    {
        var sourceType = AIDataSourceExtensions.GetSourceType(context.Model);
        var sourceHandler = _sourceHandlers.LastOrDefault(handler => string.Equals(handler.SourceType, sourceType, StringComparison.OrdinalIgnoreCase));

        if (sourceHandler is not null)
        {
            await sourceHandler.ValidateAsync(context.Model, context.Result, cancellationToken);
        }
    }
}
