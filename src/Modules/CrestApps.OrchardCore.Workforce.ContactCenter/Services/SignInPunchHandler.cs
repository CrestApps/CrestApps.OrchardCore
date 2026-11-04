using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services;

/// <summary>
/// Clocks a worker in when their agent signs in to the contact center and out when it signs out. A worker already
/// clocked in another way is left alone, and is only clocked out when the contact center clocked them in. Each punch
/// carries the event's identifier as its idempotency key, so an event delivered twice punches once.
/// </summary>
public sealed class SignInPunchHandler : IContactCenterEventHandler
{
    private readonly IWorkerManager _workerManager;
    private readonly ITimeClockService _timeClockService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignInPunchHandler"/> class.
    /// </summary>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="timeClockService">The time clock service.</param>
    /// <param name="logger">The logger.</param>
    public SignInPunchHandler(
        IWorkerManager workerManager,
        ITimeClockService timeClockService,
        ILogger<SignInPunchHandler> logger)
    {
        _workerManager = workerManager;
        _timeClockService = timeClockService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string HandlerId => "Workforce/ContactCenterSignInPunches/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.AgentStateChanged)
        {
            return;
        }

        var data = interactionEvent.GetData<AgentStateChangedEventData>();

        if (data is null || string.IsNullOrEmpty(data.UserId))
        {
            return;
        }

        var signedIn = data.Source == AgentStateChangeSources.SignIn ||
            (data.PreviousState == AgentPresenceStatus.Offline && data.CurrentState != AgentPresenceStatus.Offline);
        var signedOut = data.Source == AgentStateChangeSources.SignOut ||
            (data.CurrentState == AgentPresenceStatus.Offline && data.PreviousState != AgentPresenceStatus.Offline);

        if (signedIn == signedOut)
        {
            return;
        }

        var worker = await _workerManager.FindByUserIdAsync(data.UserId, cancellationToken);

        if (worker is null || worker.Status != WorkerStatus.Active)
        {
            return;
        }

        var status = await _timeClockService.GetStatusAsync(worker, cancellationToken);
        var at = data.ChangedUtc == default ? interactionEvent.OccurredUtc : data.ChangedUtc;
        TimePunchKind kind;

        if (signedIn)
        {
            if (status.State != TimeClockState.Out)
            {
                return;
            }

            kind = TimePunchKind.In;
        }
        else
        {
            if (status.State == TimeClockState.Out || !await ClockedInByContactCenterAsync(worker, status, cancellationToken))
            {
                return;
            }

            kind = TimePunchKind.Out;
        }

        var result = await _timeClockService.PunchAsync(new TimePunchRequest
        {
            WorkerId = worker.ItemId,
            Kind = kind,
            Source = TimePunchSource.ContactCenter,
            Utc = at,
            IdempotencyKey = "contact-center:" + interactionEvent.ItemId,
        }, data.UserId, cancellationToken);

        if (!result.Succeeded && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The contact center {Kind} punch of worker '{WorkerId}' was not recorded: {Errors}",
                kind,
                worker.ItemId,
                string.Join(" ", result.Errors));
        }
    }

    private async Task<bool> ClockedInByContactCenterAsync(WorkerProfile worker, TimeClockStatus status, CancellationToken cancellationToken)
    {
        if (status.SinceUtc is not { } since)
        {
            return status.LastPunch?.Source == TimePunchSource.ContactCenter;
        }

        // The punch that opened the current stretch is the last clock-in at or before it.
        var punches = await _timeClockService.ListAsync(worker.ItemId, since.AddDays(-1), since.AddTicks(1), cancellationToken);
        var opening = punches.LastOrDefault(punch => punch.Kind == TimePunchKind.In);

        return opening?.Source == TimePunchSource.ContactCenter;
    }
}
