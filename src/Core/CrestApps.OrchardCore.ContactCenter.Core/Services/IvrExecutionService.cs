using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Runs a caller through a phone menu: plays what the flow says is next, remembers where they are, and hands back
/// the routing decision once they have chosen.
/// </summary>
/// <remarks>
/// The caller's position is persisted on the interaction rather than held in memory, so it survives a restart and
/// so a redelivered gather event is recognised as one the caller has already been advanced by. Provider webhooks
/// are at-least-once, and acting twice on one key press takes a caller two levels into a menu they navigated once.
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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IvrExecutionService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="ivrProvider">The provider that plays the menu.</param>
    /// <param name="logger">The logger.</param>
    public IvrExecutionService(
        IInteractionManager interactionManager,
        IIvrProvider ivrProvider,
        ILogger<IvrExecutionService> logger)
    {
        _interactionManager = interactionManager;
        _ivrProvider = ivrProvider;
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
        var step = IvrFlowStateMachine.Start(flow, state);

        return await PlayAsync(interaction, flow, state, step, cancellationToken);
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
        var step = IvrFlowStateMachine.Advance(flow, state, digits, deliveryId);

        return await PlayAsync(interaction, flow, state, step, cancellationToken);
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
            return value switch
            {
                IvrFlowState state => state,
                JsonElement element => element.Deserialize<IvrFlowState>(_serializerOptions) ?? new IvrFlowState(),
                _ => JsonSerializer.Deserialize<IvrFlowState>(value.ToString(), _serializerOptions) ?? new IvrFlowState(),
            };
        }
        catch (JsonException)
        {
            // Unreadable state is the same situation as no state: start the caller at the top of the menu rather
            // than dropping a live call over a serialization problem.
            return new IvrFlowState();
        }
    }

    private async Task<IvrStep> PlayAsync(Interaction interaction, IvrFlow flow, IvrFlowState state, IvrStep step, CancellationToken cancellationToken)
    {
        if (step.Kind == IvrStepKind.Prompt)
        {
            var providerCallId = interaction.ProviderInteractionId;

            // Nothing to prompt on. Holding the caller in a menu they cannot hear would strand them, so they are
            // routed the way the entry point already said to.
            if (string.IsNullOrEmpty(providerCallId))
            {
                return IvrStep.Done;
            }

            var validDigits = BuildValidDigits(flow, step.NodeId);

            try
            {
                await _ivrProvider.PromptAsync(providerCallId, step.Prompt, step.PromptMediaId, validDigits, cancellationToken);
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

                return IvrStep.Done;
            }
        }

        await SaveStateAsync(interaction, state, cancellationToken);

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

    private async Task SaveStateAsync(Interaction interaction, IvrFlowState state, CancellationToken cancellationToken)
    {
        interaction.TechnicalMetadata ??= new Dictionary<string, object>();
        interaction.TechnicalMetadata[StateMetadataKey] = state;

        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
    }
}
