using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default <see cref="IRecordingDisclosureService"/>, reading the disclosure from the tenant's recording
/// settings.
/// </summary>
public sealed class RecordingDisclosureService : IRecordingDisclosureService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IInteractionManager _interactionManager;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingDisclosureService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider the event publisher and the recording service are resolved from when they are needed. Both reach the event handlers, and the call routing that uses this service is reached from them, so injecting either would close a construction cycle.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="siteService">The site service used to read the recording settings.</param>
    /// <param name="clock">The clock used to stamp when the disclosure was given.</param>
    /// <param name="logger">The logger.</param>
    public RecordingDisclosureService(
        IServiceProvider serviceProvider,
        IInteractionManager interactionManager,
        ISiteService siteService,
        IClock clock,
        ILogger<RecordingDisclosureService> logger)
    {
        _serviceProvider = serviceProvider;
        _interactionManager = interactionManager;
        _siteService = siteService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GetDisclosureAsync(RecordingDisclosureCallType callType, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync();

        return GetDisclosure(settings, callType);
    }

    /// <inheritdoc/>
    public async Task<bool> RecordDisclosedAsync(string interactionId, string method, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);
        ArgumentException.ThrowIfNullOrEmpty(method);

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        // Told once: the first notice is the one that counts, and a second report of it must not move the time.
        if (interaction is null || interaction.RecordingDisclosedUtc.HasValue)
        {
            return false;
        }

        var settings = await GetSettingsAsync();
        var now = _clock.UtcNow;

        interaction.RecordingDisclosedUtc = now;

        // Staying on the line after being told the call is recorded is the consent the governance policy waits for.
        // Consent captured some other way first is kept as it was.
        interaction.RecordingConsentCapturedUtc ??= now;

        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        var disclosedEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.RecordingDisclosed,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            ActorId = method == ContactCenterConstants.RecordingDisclosureMethod.Agent
                ? interaction.AgentId
                : null,
            SourceComponent = ContactCenterConstants.Components.Interactions,
        };

        disclosedEvent.SetData(new RecordingDisclosedEventData
        {
            Method = method,
            Text = settings.RecordingDisclosureText?.Trim(),
        });

        await _serviceProvider.GetRequiredService<IContactCenterEventPublisher>().PublishAsync(disclosedEvent, CancellationToken.None);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The caller on interaction {InteractionId} was told the call is recorded ({Method}).",
                interaction.ItemId.SanitizeLogValue(),
                method);
        }

        await StartRecordingHeldForConsentAsync(interaction, settings, cancellationToken);

        return true;
    }

    /// <summary>
    /// Gets the disclosure the settings give on a call of the specified type, or <see langword="null"/> when none.
    /// </summary>
    /// <param name="settings">The tenant's recording settings.</param>
    /// <param name="callType">Who gives the disclosure on the call.</param>
    /// <returns>The trimmed disclosure text, or <see langword="null"/>.</returns>
    public static string GetDisclosure(ContactCenterRecordingSettings settings, RecordingDisclosureCallType callType)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.EnableRecordingDisclosure || string.IsNullOrWhiteSpace(settings.RecordingDisclosureText))
        {
            return null;
        }

        var given = callType switch
        {
            RecordingDisclosureCallType.Inbound => settings.DiscloseOnInboundCalls,
            RecordingDisclosureCallType.AIVoiceAgent => settings.DiscloseOnAIVoiceCalls,
            RecordingDisclosureCallType.Agent => settings.PromptAgentsToDisclose,
            _ => false,
        };

        return given
            ? settings.RecordingDisclosureText.Trim()
            : null;
    }

    // A tenant that records every call but waits for consent had the recording refused when the call connected,
    // because the caller had not been told yet. Nothing else would try again, so the call would go unrecorded.
    private async Task StartRecordingHeldForConsentAsync(
        Interaction interaction,
        ContactCenterRecordingSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.RecordAllCalls ||
            !settings.RequireExplicitConsent ||
            settings.ConsentModel == RecordingConsentModel.SingleParty ||
            interaction.Channel != InteractionChannel.Voice ||
            interaction.RecordingState != RecordingState.None ||
            !string.IsNullOrEmpty(interaction.HandoffAiSessionId) ||
            interaction.IsSettled)
        {
            return;
        }

        var result = await _serviceProvider
            .GetRequiredService<IContactCenterRecordingService>()
            .StartAsync(interaction.ItemId, cancellationToken);

        if (result.Succeeded)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Started recording interaction {InteractionId} once the caller was told the call is recorded.",
                    interaction.ItemId.SanitizeLogValue());
            }

            return;
        }

        _logger.LogWarning(
            "Could not record interaction {InteractionId} after the caller was told the call is recorded: {Reason}{Unknown}.",
            interaction.ItemId.SanitizeLogValue(),
            result.Reason.SanitizeLogValue(),
            result.OutcomeUnknown ? " (the provider's outcome is unknown)" : string.Empty);
    }

    private async Task<ContactCenterRecordingSettings> GetSettingsAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        return site.GetOrCreate<ContactCenterRecordingSettings>();
    }
}
