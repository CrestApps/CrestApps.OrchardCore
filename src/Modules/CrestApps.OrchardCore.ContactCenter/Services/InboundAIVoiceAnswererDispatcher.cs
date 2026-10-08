using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Answers a call an entry point handed to its AI voice agent, once the routing that created the call's activity has
/// committed.
/// </summary>
public interface IInboundAIVoiceAnswererDispatcher
{
    /// <summary>
    /// Answers the call through the provider's AI voice answerer, and fails the activity when the provider refuses.
    /// </summary>
    /// <param name="providerName">The call's provider.</param>
    /// <param name="providerCallId">The provider's identifier for the caller's leg.</param>
    /// <param name="activityId">The automated activity the conversation is recorded on.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the answer was sent.</returns>
    Task AnswerAsync(string providerName, string providerCallId, string activityId, CancellationToken cancellationToken = default);
}

/// <inheritdoc/>
internal sealed class InboundAIVoiceAnswererDispatcher : IInboundAIVoiceAnswererDispatcher
{
    private const string AnswerRefusedReasonCode = "ai_agent_answer_refused";

    private readonly IEnumerable<IInboundAIVoiceAnswerer> _answerers;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundAIVoiceAnswererDispatcher"/> class.
    /// </summary>
    /// <param name="answerers">The AI voice answerers of the enabled providers.</param>
    /// <param name="activityManager">The activities.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public InboundAIVoiceAnswererDispatcher(
        IEnumerable<IInboundAIVoiceAnswerer> answerers,
        IOmnichannelActivityManager activityManager,
        IClock clock,
        ILogger<InboundAIVoiceAnswererDispatcher> logger)
    {
        _answerers = answerers;
        _activityManager = activityManager;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task AnswerAsync(string providerName, string providerCallId, string activityId, CancellationToken cancellationToken = default)
    {
        var answerer = _answerers.FirstOrDefault(candidate =>
            string.Equals(candidate.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        var answered = false;

        try
        {
            answered = answerer is not null && await answerer.AnswerAsync(providerCallId, activityId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Answering call '{ProviderCallId}' for its AI voice agent failed.", providerCallId.SanitizeLogValue());
        }

        if (answered)
        {
            return;
        }

        // The caller hung up while the call was being routed, or the provider refused the answer. Nobody will talk to
        // them, so the activity is closed rather than left waiting for an answer that will not come.
        _logger.LogWarning(
            "Call '{ProviderCallId}' could not be answered for its AI voice agent; activity '{ActivityId}' is marked failed.",
            providerCallId.SanitizeLogValue(),
            activityId.SanitizeLogValue());

        var activity = await _activityManager.FindByIdAsync(activityId, cancellationToken);

        if (activity is null || activity.Status.IsTerminal())
        {
            return;
        }

        activity.Status = ActivityStatus.Failed;
        activity.TerminalReasonCode = AnswerRefusedReasonCode;
        activity.CompletedUtc = _clock.UtcNow;

        await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
    }
}
