using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Tells the ticket observers (workflows, webhooks, notifications) when a ticket's service-level target is close or
/// missed. The service-level monitor raises each once per timer.
/// </summary>
public sealed class TicketServiceLevelObserver : IServiceLevelObserver
{
    private readonly IContentManager _contentManager;
    private readonly IEnumerable<ITicketObserver> _observers;
    private readonly ILogger _logger;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketServiceLevelObserver"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="observers">The ticket observers.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="services">The services, for the timeline service when a target loses its business hours.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketServiceLevelObserver(
        IContentManager contentManager,
        IEnumerable<ITicketObserver> observers,
        ILogger<TicketServiceLevelObserver> logger,
        IServiceProvider services,
        IStringLocalizer<TicketServiceLevelObserver> stringLocalizer)
    {
        _contentManager = contentManager;
        _observers = observers;
        _logger = logger;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task NoBusinessHoursAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(timer?.Subject.Kind, TicketsConstants.RecordKind, StringComparison.Ordinal))
        {
            return;
        }

        // The ticket's history says why its target now counts around the clock. The timeline service is resolved here:
        // it reaches the record sources, which lead back to the ticket service that constructs this observer's tracker.
        await _services.GetRequiredService<ITimelineService>().AddAsync(new TimelineEntry
        {
            Subject = timer.Subject,
            Kind = TicketsConstants.EntryKinds.Event,
            Visibility = TimelineVisibility.Internal,
            Body = S["The business hours of the service level are never open now, so its target counts around the clock until they open again."],
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task WarningAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        => RaiseAsync(timer, breached: false, cancellationToken);

    /// <inheritdoc/>
    public Task BreachedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        => RaiseAsync(timer, breached: true, cancellationToken);

    private async Task RaiseAsync(ServiceLevelTimer timer, bool breached, CancellationToken cancellationToken)
    {
        if (!string.Equals(timer?.Subject.Kind, TicketsConstants.RecordKind, StringComparison.Ordinal)
            || await _contentManager.GetAsync(timer.Subject.Id, VersionOptions.Latest) is not { } ticket
            || !ticket.Has<TicketPart>())
        {
            return;
        }

        foreach (var observer in _observers)
        {
            try
            {
                await observer.ServiceLevelAsync(ticket, timer.Metric, breached, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "A ticket observer failed to hear of a service-level target.");
            }
        }
    }
}
