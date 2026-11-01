using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITimeOffAllotmentManager"/>.
/// </summary>
internal sealed class TimeOffAllotmentManager : CatalogManager<TimeOffAllotment>, ITimeOffAllotmentManager
{
    public TimeOffAllotmentManager(
        TimeOffAllotmentStore store,
        IEnumerable<ICatalogEntryHandler<TimeOffAllotment>> handlers,
        ILogger<CatalogManager<TimeOffAllotment>> logger)
        : base(store, handlers, logger)
    {
    }
}
