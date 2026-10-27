using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Security;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Chat.Core.Services;

/// <summary>
/// Runs the default output filter, then the profile's guardrails on the AI's web chat reply. The chat hub shows the
/// profile's placeholder reply in place of a reply it blocks.
/// </summary>
public sealed class AIGuardrailOutputSecurityFilter : IOutputSecurityFilter
{
    private readonly DefaultOutputSecurityFilter _inner;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAIProfileManager _profileManager;
    private readonly AIGuardrailService _guardrails;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIGuardrailOutputSecurityFilter"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider the default filter is created from.</param>
    /// <param name="sessionManager">The chat session manager.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="guardrails">The guardrail service.</param>
    public AIGuardrailOutputSecurityFilter(
        IServiceProvider serviceProvider,
        IAIChatSessionManager sessionManager,
        IAIProfileManager profileManager,
        AIGuardrailService guardrails)
    {
        _inner = ActivatorUtilities.CreateInstance<DefaultOutputSecurityFilter>(serviceProvider);
        _sessionManager = sessionManager;
        _profileManager = profileManager;
        _guardrails = guardrails;
    }

    /// <inheritdoc/>
    public async Task<PromptSecurityResult> ValidateOutputAsync(OutputSecurityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = await _inner.ValidateOutputAsync(context, cancellationToken);

        if (result.IsBlocked || string.IsNullOrEmpty(context.SessionId) || string.IsNullOrWhiteSpace(context.Output))
        {
            return result;
        }

        // The session was loaded by the same request, so these reads come from the session's identity map.
        var session = await _sessionManager.FindByIdAsync(context.SessionId, cancellationToken);
        var profile = string.IsNullOrEmpty(session?.ProfileId) ? null : await _profileManager.FindByIdAsync(session.ProfileId, cancellationToken);

        if (!AIGuardrailService.IsOn(profile, AIGuardrailDirection.Reply))
        {
            return result;
        }

        var verdict = await _guardrails.CheckAsync(profile, context.Output, AIGuardrailDirection.Reply, context.SessionId, cancellationToken);

        return verdict is null
            ? result
            : PromptSecurityResult.Blocked(verdict.Reason, PromptRiskLevel.High, "guardrail:" + verdict.Category);
    }
}
