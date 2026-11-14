using CrestApps.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// The service-level scope of tickets: a first response, each next response and the resolution, with a target per
/// ticket priority, most urgent first.
/// </summary>
public sealed class TicketServiceLevelScope : IServiceLevelScopeProvider
{
    private static readonly LocalizationSource _displayName = LocalizationSource.Create<TicketServiceLevelScope>("Tickets");

    private readonly ICatalog<TicketPriority> _priorities;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketServiceLevelScope"/> class.
    /// </summary>
    /// <param name="priorities">The ticket priorities.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketServiceLevelScope(ICatalog<TicketPriority> priorities, IStringLocalizer<TicketServiceLevelScope> stringLocalizer)
    {
        _priorities = priorities;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => TicketsConstants.ServiceLevelScope;

    /// <inheritdoc/>
    public LocalizationSource DisplayName => _displayName;

    /// <inheritdoc/>
    public Task<IReadOnlyList<ServiceLevelOption>> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ServiceLevelOption> metrics =
        [
            new(ServiceLevelMetrics.FirstResponse, S["First response"]),
            new(ServiceLevelMetrics.NextResponse, S["Next response"]),
            new(ServiceLevelMetrics.Resolution, S["Resolution"]),
        ];

        return Task.FromResult(metrics);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceLevelOption>> GetPrioritiesAsync(CancellationToken cancellationToken = default)
        => [.. (await _priorities.GetAllAsync(cancellationToken))
            .OrderByDescending(priority => priority.Rank)
            .Select(priority => new ServiceLevelOption(priority.Name, priority.Name))];
}
