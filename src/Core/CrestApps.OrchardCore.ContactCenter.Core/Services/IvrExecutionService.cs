using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Runs a caller through a phone menu: plays what the flow says is next, remembers where they are, and hands back
/// the routing decision once they have chosen.
/// </summary>
/// <remarks>
/// The caller's position is persisted on the interaction rather than held in memory, so it survives a restart and
/// so a redelivered gather event is recognised as one the caller has already been advanced by. Provider webhooks
/// are at-least-once, and acting twice on one key press takes a caller two levels into a menu they navigated once.
/// <para>
/// The position is committed before the provider is asked to play anything. The provider's own events for the
/// call (the answer, the next collection) arrive as separate webhooks that write the same interaction, and a
/// position still uncommitted when they land is lost to a concurrency conflict after the caller has already heard
/// the menu it describes.
/// </para>
/// </remarks>
public sealed class IvrExecutionService : IIvrExecutionService
{
    /// <summary>
    /// The technical-metadata key the caller's menu position is stored under.
    /// </summary>
    public const string StateMetadataKey = "ivrFlowState";

    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IInteractionManager _interactionManager;
    private readonly IIvrProvider _ivrProvider;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IvrExecutionService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="ivrProvider">The provider that plays the menu.</param>
    /// <param name="auditRecorder">The recorder the caller's route through the menu is written to.</param>
    /// <param name="session">The session the caller's position is committed on before the provider is asked to play.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public IvrExecutionService(
        IInteractionManager interactionManager,
        IIvrProvider ivrProvider,
        IContactCenterAuditRecorder auditRecorder,
        ISession session,
        IClock clock,
        ILogger<IvrExecutionService> logger)
    {
        _interactionManager = interactionManager;
        _ivrProvider = ivrProvider;
        _auditRecorder = auditRecorder;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IvrStep> StartAsync(Interaction interaction, IvrFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        // No menu on this entry point: it routes exactly the way it did before flows existed, which is what every
        // tenant that has not configured one expects.
        if (flow is null)
        {
            return IvrStep.Done;
        }

        var state = ReadState(interaction);

        // A menu that has already run is not started again: a redelivered inbound event must not answer the caller
        // a second time and play them the top of a menu they have already left.
        if (state.Completed)
        {
            return IvrStep.Ignored;
        }

        var step = IvrFlowStateMachine.Start(flow, state);

        if (step.Kind != IvrStepKind.Prompt)
        {
            return step;
        }

        // Nothing to prompt on. Holding the caller in a menu they cannot hear would strand them, so they are
        // routed the way the entry point already said to.
        if (string.IsNullOrEmpty(interaction.ProviderInteractionId))
        {
            return IvrStep.Done;
        }

        var now = _clock.UtcNow;
        AppendPath(state, step.NodeId, digits: null, "Menu", now);
        await CommitStateAsync(interaction, state, cancellationToken);

        // The caller's leg is still ringing. A leg that is already up refuses the answer, which is not a reason to
        // stop: the menu is what matters, and the prompt below reports whether the leg is really there.
        if (!await _ivrProvider.AnswerAsync(interaction.ProviderInteractionId, cancellationToken) &&
            _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "The provider did not answer call '{ProviderCallId}' for the entry-point menu; it may already be answered.",
                interaction.ProviderInteractionId.SanitizeLogValue());
        }

        return await PromptAsync(interaction, flow, state, step, deliveryId: null, now, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IvrStep> HandleDigitsAsync(
        Interaction interaction,
        IvrFlow flow,
        string digits,
        string deliveryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (flow is null)
        {
            return IvrStep.Done;
        }

        var state = ReadState(interaction);

        if (state.Completed)
        {
            return IvrStep.Ignored;
        }

        var nodeId = state.CurrentNodeId;
        var redelivered = !string.IsNullOrEmpty(deliveryId) &&
            state.AppliedDeliveryIds.Contains(deliveryId, StringComparer.Ordinal);
        var step = IvrFlowStateMachine.Advance(flow, state, digits, deliveryId);

        if (step.Kind == IvrStepKind.Ignored)
        {
            return step;
        }

        var now = _clock.UtcNow;

        if (!redelivered)
        {
            AppendPath(state, nodeId, string.IsNullOrWhiteSpace(digits) ? string.Empty : digits.Trim(), Describe(step), now);
        }

        // Every outcome other than another menu takes the caller out of the menu.
        state.Completed = step.Kind != IvrStepKind.Prompt;

        // Only another menu needs the position committed first, because only a menu is followed by a provider
        // command. A routing outcome is left to commit with the routing it causes: committed on its own, a routing
        // that then failed on a concurrency conflict was retried against a caller already marked as out of the menu,
        // and the retry did nothing.
        if (step.Kind == IvrStepKind.Prompt)
        {
            await CommitStateAsync(interaction, state, cancellationToken);
        }
        else
        {
            SaveState(interaction, state);
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        if (!redelivered)
        {
            await _auditRecorder.RecordIvrAsync(
                ContactCenterConstants.Events.IvrDigitsReceived,
                interaction,
                new IvrAuditStep
                {
                    NodeId = nodeId,
                    Digits = string.IsNullOrWhiteSpace(digits) ? null : digits.Trim(),
                    Reason = string.IsNullOrWhiteSpace(digits) ? "NoInput" : "KeyPressed",
                    EntryPointId = ReadEntryPointId(interaction),
                },
                now,
                $"ivr:digits:{interaction.ItemId}:{deliveryId ?? Stamp(now)}",
                cancellationToken);
        }

        if (step.Kind == IvrStepKind.Prompt)
        {
            return await PromptAsync(interaction, flow, state, step, deliveryId, now, cancellationToken);
        }

        if (!redelivered)
        {
            await _auditRecorder.RecordIvrAsync(
                step.IsFallback ? ContactCenterConstants.Events.IvrFallbackTaken : ContactCenterConstants.Events.IvrActionTaken,
                interaction,
                new IvrAuditStep
                {
                    NodeId = nodeId,
                    Digits = string.IsNullOrWhiteSpace(digits) ? null : digits.Trim(),
                    Action = step.Kind.ToString(),
                    Target = step.TargetId,
                    Reason = step.IsFallback ? "RetriesExhausted" : "CallerChoice",
                    EntryPointId = ReadEntryPointId(interaction),
                },
                now,
                $"ivr:action:{interaction.ItemId}:{deliveryId ?? Stamp(now)}",
                cancellationToken);
        }

        return step;
    }

    /// <inheritdoc/>
    public async Task EndAsync(Interaction interaction, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var state = ReadState(interaction);

        if (state.Completed)
        {
            return;
        }

        state.Completed = true;
        AppendPath(state, state.CurrentNodeId, digits: null, reason, _clock.UtcNow);

        interaction.TechnicalMetadata ??= new Dictionary<string, object>();
        interaction.TechnicalMetadata[StateMetadataKey] = state;

        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Reads the caller's menu position off the interaction, or starts a fresh one.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    public static IvrFlowState ReadState(Interaction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (interaction.TechnicalMetadata is null ||
            !interaction.TechnicalMetadata.TryGetValue(StateMetadataKey, out var value) ||
            value is null)
        {
            return new IvrFlowState();
        }

        try
        {
            // The store hands a reloaded value back in whatever shape its serializer gives an untyped value (an
            // ExpandoObject from YesSql, a JsonElement or JsonNode elsewhere). Reading anything but the typed state or
            // a JSON string through ToString() threw, so every reloaded caller was treated as having no position and
            // their next key press sent them to the entry point's target instead of down the menu.
            return value switch
            {
                IvrFlowState state => state,
                JsonElement element => element.Deserialize<IvrFlowState>(_serializerOptions) ?? new IvrFlowState(),
                string json => JsonSerializer.Deserialize<IvrFlowState>(json, _serializerOptions) ?? new IvrFlowState(),
                _ => JsonSerializer.Deserialize<IvrFlowState>(JsonSerializer.Serialize(value, value.GetType(), _serializerOptions), _serializerOptions) ?? new IvrFlowState(),
            };
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Unreadable state is the same situation as no state: start the caller at the top of the menu rather
            // than dropping a live call over a serialization problem.
            return new IvrFlowState();
        }
    }

    private async Task<IvrStep> PromptAsync(
        Interaction interaction,
        IvrFlow flow,
        IvrFlowState state,
        IvrStep step,
        string deliveryId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var played = false;

        try
        {
            played = await _ivrProvider.PromptAsync(
                interaction.ProviderInteractionId,
                step.Prompt,
                step.PromptMediaId,
                BuildValidDigits(flow, step.NodeId),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A caller who hung up mid-menu must not surface as an unhandled failure on the inbound path.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "The IVR prompt could not be played on interaction '{InteractionId}'.", interaction.ItemId.SanitizeLogValue());
            }
        }

        if (!played)
        {
            // A menu the caller cannot hear is no menu at all: they are put through the way the entry point routes
            // callers with no menu, rather than being held on a silent line waiting for a key press that cannot come.
            _logger.LogWarning(
                "The entry-point menu '{NodeId}' could not be played on interaction '{InteractionId}'; the caller is routed to the entry point's target.",
                step.NodeId.SanitizeLogValue(),
                interaction.ItemId.SanitizeLogValue());

            // The position was committed before the prompt, and a commit forgets what the session had loaded, so the
            // interaction is read again rather than saved from the copy taken before.
            var current = await _interactionManager.FindByIdAsync(interaction.ItemId, cancellationToken) ?? interaction;
            var currentState = ReadState(current);
            currentState.Completed = true;
            AppendPath(currentState, step.NodeId, digits: null, "PromptFailed", now);
            current.TechnicalMetadata ??= new Dictionary<string, object>();
            current.TechnicalMetadata[StateMetadataKey] = currentState;
            await _interactionManager.UpdateAsync(current, cancellationToken: cancellationToken);

            return IvrStep.Done with { IsFallback = true };
        }

        await _auditRecorder.RecordIvrAsync(
            ContactCenterConstants.Events.IvrMenuEntered,
            interaction,
            new IvrAuditStep
            {
                NodeId = step.NodeId,
                Reason = step.IsRetry ? "Retry" : "Menu",
                Attempt = state.Attempts + 1,
                EntryPointId = ReadEntryPointId(interaction),
            },
            now,
            $"ivr:menu:{interaction.ItemId}:{step.NodeId}:{deliveryId ?? "start"}",
            cancellationToken);

        return step;
    }

    // The digits this menu accepts, so the provider collects a key that means something rather than any key at
    // all and then reporting one the flow has to reject.
    private static string BuildValidDigits(IvrFlow flow, string nodeId)
    {
        var node = flow.Nodes?.FirstOrDefault(candidate => string.Equals(candidate?.NodeId, nodeId, StringComparison.Ordinal));

        if (node?.Options is null || node.Options.Count == 0)
        {
            return null;
        }

        return string.Concat(node.Options
            .Select(option => option?.Digit)
            .Where(digit => !string.IsNullOrWhiteSpace(digit))
            .Select(digit => digit.Trim()));
    }

    private static void SaveState(Interaction interaction, IvrFlowState state)
    {
        interaction.TechnicalMetadata ??= new Dictionary<string, object>();
        interaction.TechnicalMetadata[StateMetadataKey] = state;
    }

    private async Task CommitStateAsync(Interaction interaction, IvrFlowState state, CancellationToken cancellationToken)
    {
        SaveState(interaction, state);

        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        // Committed before the provider is asked to do anything; see the remarks on this class.
        await _session.SaveChangesAsync(cancellationToken);
    }

    private static void AppendPath(IvrFlowState state, string nodeId, string digits, string result, DateTime occurredUtc)
    {
        state.Path ??= [];
        state.Path.Add(new IvrPathEntry
        {
            NodeId = nodeId,
            Digits = digits,
            Result = result,
            OccurredUtc = occurredUtc,
        });
    }

    private static string Describe(IvrStep step)
    {
        var description = step.Kind switch
        {
            IvrStepKind.Prompt when step.IsRetry => "Retry",
            IvrStepKind.Prompt => $"Menu:{step.NodeId}",
            IvrStepKind.Done => "EntryPointTarget",
            _ when string.IsNullOrEmpty(step.TargetId) => step.Kind.ToString(),
            _ => $"{step.Kind}:{step.TargetId}",
        };

        return step.IsFallback ? $"Fallback:{description}" : description;
    }

    private static string ReadEntryPointId(Interaction interaction)
        => interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(EntryPointFlowResolver.EntryPointMetadataKey, out var value)
            ? value?.ToString()
            : null;

    private static string Stamp(DateTime value)
        => value.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
