using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Turns an extension typed into the soft phone's transfer panel on a Contact Center call into the agent it rings, so
/// the transfer is routed exactly as picking that agent from the directory would route it.
/// </summary>
internal interface ISoftPhoneExtensionTransferTargetResolver
{
    /// <summary>
    /// Resolves an extension to the Contact Center agent it rings.
    /// </summary>
    /// <param name="extension">The extension the agent typed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent, or why the extension cannot be transferred to.</returns>
    Task<SoftPhoneExtensionTransferTarget> ResolveAsync(string extension, CancellationToken cancellationToken = default);
}

/// <summary>
/// The agent an extension rings, or why it cannot be transferred to.
/// </summary>
internal sealed class SoftPhoneExtensionTransferTarget
{
    private SoftPhoneExtensionTransferTarget(string agentId, string error)
    {
        AgentId = agentId;
        Error = error;
    }

    /// <summary>Gets a value indicating whether the extension belongs to an agent.</summary>
    public bool Succeeded => !string.IsNullOrEmpty(AgentId);

    /// <summary>Gets the agent the extension rings.</summary>
    public string AgentId { get; }

    /// <summary>Gets why the extension cannot be transferred to.</summary>
    public string Error { get; }

    /// <summary>Creates a target for the agent the extension rings.</summary>
    /// <param name="agentId">The agent profile identifier.</param>
    public static SoftPhoneExtensionTransferTarget Agent(string agentId)
        => new(agentId, null);

    /// <summary>Creates a refusal.</summary>
    /// <param name="error">Why the extension cannot be transferred to.</param>
    public static SoftPhoneExtensionTransferTarget Refused(string error)
        => new(null, error);
}

/// <summary>
/// Resolves an extension through the telephony extension registry, then to the agent profile of the user it rings.
/// </summary>
/// <remarks>
/// An extension that rings somebody who is not an agent is refused rather than dialed: the Contact Center routes,
/// moves and records a transfer only to its own agents, queues and numbers, and a call handed to anyone else would
/// leave the interaction with nobody accountable for it.
/// </remarks>
internal sealed class SoftPhoneExtensionTransferTargetResolver : ISoftPhoneExtensionTransferTargetResolver
{
    private readonly ITelephonyExtensionResolver _extensionResolver;
    private readonly IAgentProfileManager _agentManager;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhoneExtensionTransferTargetResolver"/> class.
    /// </summary>
    /// <param name="extensionResolvers">The telephony extension registry, when extensions are available.</param>
    /// <param name="agentManager">The agent profiles.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SoftPhoneExtensionTransferTargetResolver(
        IEnumerable<ITelephonyExtensionResolver> extensionResolvers,
        IAgentProfileManager agentManager,
        IStringLocalizer<SoftPhoneExtensionTransferTargetResolver> stringLocalizer)
    {
        _extensionResolver = extensionResolvers?.FirstOrDefault();
        _agentManager = agentManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<SoftPhoneExtensionTransferTarget> ResolveAsync(string extension, CancellationToken cancellationToken = default)
    {
        var number = extension?.Trim();

        if (string.IsNullOrEmpty(number) || number.Length > 15 || !number.All(char.IsAsciiDigit))
        {
            return SoftPhoneExtensionTransferTarget.Refused(S["Enter the extension as digits only."].Value);
        }

        if (_extensionResolver is null)
        {
            return SoftPhoneExtensionTransferTarget.Refused(S["Extensions are not available. Choose an agent, a queue or a number."].Value);
        }

        var resolution = await _extensionResolver.ResolveAsync(number, cancellationToken);

        if (!resolution.Found)
        {
            return SoftPhoneExtensionTransferTarget.Refused(S["Extension {0} was not found.", number].Value);
        }

        var agent = await _agentManager.FindByUserIdAsync(resolution.UserId, cancellationToken);

        return agent is null || string.IsNullOrEmpty(agent.ItemId)
            ? SoftPhoneExtensionTransferTarget.Refused(S["Extension {0} is not a Contact Center agent. Choose an agent, a queue or a number.", number].Value)
            : SoftPhoneExtensionTransferTarget.Agent(agent.ItemId);
    }
}
