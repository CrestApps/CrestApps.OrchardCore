using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Manages blackout dates.
/// </summary>
public interface IBlackoutPeriodManager : ICatalogManager<BlackoutPeriod>
{
}
