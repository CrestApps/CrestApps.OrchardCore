using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Checks.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checks.Core.Handlers;

/// <summary>
/// Stamps the changes of checks.
/// </summary>
public sealed class CheckRecordHandler : CatalogEntryHandlerBase<CheckRecord>
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckRecordHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public CheckRecordHandler(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<CheckRecord> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }
}
