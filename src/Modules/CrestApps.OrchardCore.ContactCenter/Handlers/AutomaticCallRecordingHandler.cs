using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Starts recording a voice call the moment it connects, when the tenant records every call.
/// </summary>
/// <remarks>
/// <para>
/// "Recording enabled" only permits recording; before this handler nothing started one unless a workflow or a
/// supervisor asked, so a tenant that turned recording on got no recordings at all. A call is recorded from the moment
/// it is bridged to an agent (<see cref="ContactCenterConstants.Events.CallConnected"/>), so the queue's hold music is
/// not captured.
/// </para>
/// <para>
/// An automated voice agent's call is recorded by its voice provider from the moment it is answered, because it is
/// often never an interaction. When the AI hands it to a person, that recording is still running, so the hand-off's
/// connect does not start a second one.
/// </para>
/// <para>
/// A call only ever starts recording from <see cref="RecordingState.None"/>. A call returning from hold raises
/// <see cref="ContactCenterConstants.Events.CallConnected"/> again, and starting from any other state would resume a
/// secure pause the agent is relying on, or restart a recording someone deliberately stopped.
/// </para>
/// </remarks>
public sealed class AutomaticCallRecordingHandler : IContactCenterEventHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomaticCallRecordingHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider the recording service is resolved from when it is needed. It reaches the event publisher, so injecting it would close a construction cycle through the outbox and its handlers.</param>
    /// <param name="siteService">The site service used to read the recording settings.</param>
    /// <param name="logger">The logger.</param>
    public AutomaticCallRecordingHandler(
        IServiceProvider serviceProvider,
        ISiteService siteService,
        ILogger<AutomaticCallRecordingHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _siteService = siteService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/AutomaticCallRecording/v1";

    /// <inheritdoc/>
    // Starting a recording that is already running is refused by the recording service, which reads the persisted
    // recording state, and this handler only acts on an interaction that has never recorded.
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.GuardedByDurableStore;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.CallConnected)
        {
            return;
        }

        if (string.IsNullOrEmpty(interactionEvent.InteractionId))
        {
            return;
        }

        var settings = (await _siteService.GetSiteSettingsAsync()).GetOrCreate<ContactCenterRecordingSettings>();

        if (!settings.RecordingEnabled || !settings.RecordAllCalls)
        {
            return;
        }

        var interactionManager = _serviceProvider.GetRequiredService<IInteractionManager>();
        var interaction = await interactionManager.FindByIdAsync(interactionEvent.InteractionId, cancellationToken);

        if (interaction is null ||
            interaction.Channel != InteractionChannel.Voice ||
            interaction.RecordingState != RecordingState.None ||
            !string.IsNullOrEmpty(interaction.HandoffAiSessionId) ||
            interaction.Status is InteractionStatus.Ended or InteractionStatus.Failed)
        {
            return;
        }

        var recordingService = _serviceProvider.GetRequiredService<IContactCenterRecordingService>();
        var result = await recordingService.StartAsync(interaction.ItemId, cancellationToken);

        if (result.Succeeded)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Started recording interaction {InteractionId} automatically when it connected.",
                    interaction.ItemId.SanitizeLogValue());
            }

            return;
        }

        // A refused start is not retried: the call goes on unrecorded, so say why where an operator will look.
        _logger.LogWarning(
            "Could not record interaction {InteractionId} automatically when it connected: {Reason}{Unknown}.",
            interaction.ItemId.SanitizeLogValue(),
            result.Reason.SanitizeLogValue(),
            result.OutcomeUnknown ? " (the provider's outcome is unknown)" : string.Empty);
    }
}
