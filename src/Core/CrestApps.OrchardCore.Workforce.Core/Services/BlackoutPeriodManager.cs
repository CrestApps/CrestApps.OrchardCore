using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IBlackoutPeriodManager"/>.
/// </summary>
internal sealed class BlackoutPeriodManager : CatalogManager<BlackoutPeriod>, IBlackoutPeriodManager
{
    public BlackoutPeriodManager(
        BlackoutPeriodStore store,
        IEnumerable<ICatalogEntryHandler<BlackoutPeriod>> handlers,
        ILogger<CatalogManager<BlackoutPeriod>> logger)
        : base(store, handlers, logger)
    {
    }
}
