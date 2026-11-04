using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IShiftTemplateManager"/>.
/// </summary>
internal sealed class ShiftTemplateManager : CatalogManager<ShiftTemplate>, IShiftTemplateManager
{
    private readonly ShiftTemplateStore _store;

    public ShiftTemplateManager(
        ShiftTemplateStore store,
        IEnumerable<ICatalogEntryHandler<ShiftTemplate>> handlers,
        ILogger<CatalogManager<ShiftTemplate>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<ShiftTemplate>> ListForTeamAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var templates = await _store.ListAsync(cancellationToken);
        var result = templates.Where(template => template.Enabled && (template.TeamIds.Count == 0 || template.TeamIds.Contains(teamId))).ToArray();

        foreach (var item in result)
        {
            await LoadAsync(item, cancellationToken);
        }

        return result;
    }
}
