using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IEntryPointResolver"/>.
/// </summary>
public sealed class EntryPointResolver : IEntryPointResolver
{
    private readonly IContactCenterEntryPointManager _entryPointManager;
    private readonly IBusinessHoursService _businessHours;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntryPointResolver"/> class.
    /// </summary>
    /// <param name="entryPointManager">The entry point manager.</param>
    /// <param name="businessHours">The business-hours service used to evaluate open/closed state.</param>
    public EntryPointResolver(
        IContactCenterEntryPointManager entryPointManager,
        IBusinessHoursService businessHours)
    {
        _entryPointManager = entryPointManager;
        _businessHours = businessHours;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterEntryPoint> FindByDialedNumberAsync(string dialedNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(dialedNumber))
        {
            return null;
        }

        var entryPoints = await _entryPointManager.ListEnabledAsync(cancellationToken);

        return entryPoints.FirstOrDefault(entryPoint => entryPoint.DialedNumbers is not null &&
            entryPoint.DialedNumbers.Any(number => string.Equals(number?.Trim(), dialedNumber, StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc/>
    public async Task<EntryPointRoutingPlan> ResolveAsync(string dialedNumber, CancellationToken cancellationToken = default)
    {
        var entryPoint = await FindByDialedNumberAsync(dialedNumber, cancellationToken);

        if (entryPoint is null)
        {
            return null;
        }

        var isOpen = await _businessHours.IsOpenAsync(entryPoint.BusinessHoursCalendarId, cancellationToken);

        return EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen);
    }
}
