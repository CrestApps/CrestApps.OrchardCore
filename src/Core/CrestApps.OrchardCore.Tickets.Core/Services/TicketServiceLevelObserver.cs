using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketServiceLevelObserver"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="observers">The ticket observers.</param>
    /// <param name="logger">The logger.</param>
    public TicketServiceLevelObserver(IContentManager contentManager, IEnumerable<ITicketObserver> observers, ILogger<TicketServiceLevelObserver> logger)
    {
        _contentManager = contentManager;
        _observers = observers;
        _logger = logger;
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
