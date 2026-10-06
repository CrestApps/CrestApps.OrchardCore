using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telnyx.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Default implementation of <see cref="ITelnyxNoiseSuppressionService"/>. It issues Telnyx's
/// <c>suppression_start</c> call command once a leg is connected, so the background noise of a call center floor is
/// cleaned out of the agent's voice before the customer hears it.
/// </summary>
/// <remarks>
/// <para>
/// Suppression belongs to the leg, not to the bridge, so it is started once on the agent's leg and keeps working when
/// that leg is later held, moved into a conference, or bridged to somebody else. The command carries a
/// <c>command_id</c> made from the leg, and Telnyx ignores a command repeated with the same id on the same call, so
/// starting it again for a leg that is bridged a second time is harmless.
/// </para>
/// <para>
/// It is best effort. The call works without it, so a refusal is logged with what Telnyx said and the call goes on.
/// </para>
/// </remarks>
public sealed class TelnyxNoiseSuppressionService : ITelnyxNoiseSuppressionService
{
    /// <summary>
    /// The Telnyx call command that starts noise suppression on a leg.
    /// </summary>
    public const string StartAction = "suppression_start";

    private readonly TelnyxApiClient _apiClient;
    private readonly IOptionsMonitor<TelnyxOptions> _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxNoiseSuppressionService"/> class.
    /// </summary>
    /// <param name="apiClient">The Telnyx API client the command is sent with.</param>
    /// <param name="options">The Telnyx settings, read again for every leg so a saved change applies to the next call.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxNoiseSuppressionService(
        TelnyxApiClient apiClient,
        IOptionsMonitor<TelnyxOptions> options,
        ILogger<TelnyxNoiseSuppressionService> logger)
    {
        _apiClient = apiClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ApplyAsync(string callControlId, TelnyxNoiseSuppressionLeg leg, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callControlId))
        {
            return;
        }

        var options = _options.CurrentValue;

        if (!options.IsConfigured)
        {
            return;
        }

        var engine = GetEngineName(options.NoiseSuppressionEngine);
        var direction = GetDirection(leg, options.NoiseSuppressionAgentVoice, options.NoiseSuppressionCallerVoice);

        if (engine is null || direction is null)
        {
            return;
        }

        var legId = callControlId.Trim();

        try
        {
            var result = await _apiClient.PostCallActionAsync(legId, StartAction, BuildStartBody(legId, engine, direction), cancellationToken);

            if (!result.Succeeded)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "Telnyx refused to start noise suppression {Engine} on {Leg} leg {CallControlId} ({Direction}) with status code {StatusCode}; the call goes on without it. Response: {Response}",
                        engine,
                        leg,
                        legId.SanitizeLogValue(),
                        direction,
                        result.StatusCode,
                        result.ErrorBody.SanitizeLogValue());
                }

                return;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Telnyx noise suppression {Engine} started on {Leg} leg {CallControlId} ({Direction})",
                    engine,
                    leg,
                    legId.SanitizeLogValue(),
                    direction);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "An error occurred while starting noise suppression {Engine} on {Leg} leg {CallControlId}; the call goes on without it.",
                engine,
                leg,
                legId.SanitizeLogValue());
        }
    }

    /// <summary>
    /// Builds the body of the <c>suppression_start</c> command.
    /// </summary>
    /// <param name="callControlId">The leg the command is issued on, which the <c>command_id</c> is made from.</param>
    /// <param name="engine">The Telnyx engine name.</param>
    /// <param name="direction">The Telnyx direction.</param>
    /// <returns>The command body.</returns>
    public static IDictionary<string, object> BuildStartBody(string callControlId, string engine, string direction)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["direction"] = direction,
            ["noise_suppression_engine"] = engine,
            // Telnyx ignores a command repeated with the same command_id on the same call, so a leg bridged again
            // (after a hold, a transfer, or a redelivered webhook) is not started twice.
            ["command_id"] = $"noise-suppression-{callControlId}",
        };

    /// <summary>
    /// Returns the Telnyx name of the engine, or <see langword="null"/> when suppression is off or the value is not one
    /// this platform knows.
    /// </summary>
    /// <param name="engine">The configured engine.</param>
    public static string GetEngineName(TelnyxNoiseSuppressionEngine engine)
        => engine switch
        {
            TelnyxNoiseSuppressionEngine.Krisp => "Krisp",
            TelnyxNoiseSuppressionEngine.DeepFilterNet => "DeepFilterNet",
            TelnyxNoiseSuppressionEngine.Denoiser => "Denoiser",
            TelnyxNoiseSuppressionEngine.AiCoustics => "AiCoustics",
            _ => null,
        };

    /// <summary>
    /// Returns the Telnyx direction that cleans the selected voices on the given leg, or <see langword="null"/> when
    /// there is nothing to clean on it.
    /// </summary>
    /// <remarks>
    /// Telnyx names the direction from its own side of the leg: <c>outbound</c> cleans what it receives from the party the
    /// leg reaches, <c>inbound</c> what it plays to them. On the agent's leg the agent's voice is <c>outbound</c> and the
    /// caller's <c>inbound</c>; on the customer's leg it is the other way round.
    /// </remarks>
    /// <param name="leg">Whose leg it is.</param>
    /// <param name="agentVoice">Whether the agent's voice, which the caller hears, is cleaned.</param>
    /// <param name="callerVoice">Whether the caller's voice, which the agent hears, is cleaned.</param>
    public static string GetDirection(TelnyxNoiseSuppressionLeg leg, bool agentVoice, bool callerVoice)
    {
        if (leg == TelnyxNoiseSuppressionLeg.InternalAgent)
        {
            // The colleague's own leg cleans the colleague's voice.
            callerVoice = false;
        }

        var (receivedFromParty, playedToParty) = leg == TelnyxNoiseSuppressionLeg.Customer
            ? (callerVoice, agentVoice)
            : (agentVoice, callerVoice);

        return (receivedFromParty, playedToParty) switch
        {
            (true, true) => "both",
            (true, false) => "outbound",
            (false, true) => "inbound",
            _ => null,
        };
    }

    /// <summary>
    /// Reads a stored or submitted engine value, treating anything this platform does not know as off.
    /// </summary>
    /// <param name="value">The engine name or number.</param>
    public static TelnyxNoiseSuppressionEngine NormalizeEngine(string value)
        => !string.IsNullOrWhiteSpace(value) &&
            Enum.TryParse<TelnyxNoiseSuppressionEngine>(value.Trim(), ignoreCase: true, out var engine)
            ? NormalizeEngine(engine)
            : TelnyxNoiseSuppressionEngine.Off;

    /// <summary>
    /// Returns the engine when it is one this platform knows, otherwise <see cref="TelnyxNoiseSuppressionEngine.Off"/>.
    /// </summary>
    /// <param name="engine">The engine.</param>
    public static TelnyxNoiseSuppressionEngine NormalizeEngine(TelnyxNoiseSuppressionEngine engine)
        => Enum.IsDefined(engine) ? engine : TelnyxNoiseSuppressionEngine.Off;
}
