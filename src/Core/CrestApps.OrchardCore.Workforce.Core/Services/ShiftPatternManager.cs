using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IShiftPatternManager"/>.
/// </summary>
internal sealed class ShiftPatternManager : CatalogManager<ShiftPattern>, IShiftPatternManager
{
    private readonly ShiftPatternStore _store;

    public ShiftPatternManager(
        ShiftPatternStore store,
        IEnumerable<ICatalogEntryHandler<ShiftPattern>> handlers,
        ILogger<CatalogManager<ShiftPattern>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<ShiftPattern>> ListForTeamAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var result = await _store.ListForTeamAsync(teamId, cancellationToken);

        foreach (var item in result)
        {
            await LoadAsync(item, cancellationToken);
        }

        return result;
    }
}
