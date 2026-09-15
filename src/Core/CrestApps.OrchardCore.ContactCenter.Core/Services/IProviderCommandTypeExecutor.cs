using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Handles provider-type-specific dispatch logic for a single <see cref="ProviderCommandType"/>.
/// The processor owns the state machine, claims, reconciliation, compensation, and session commit;
/// the executor owns request preparation, provider execution, and outcome projections.
/// </summary>
public interface IProviderCommandTypeExecutor
{
    /// <summary>
    /// Gets the <see cref="ProviderCommandType"/> this executor handles.
    /// </summary>
    ProviderCommandType CommandType { get; }

    /// <summary>
    /// Determines whether the command may still be dispatched to the provider.
    /// </summary>
    /// <param name="command">The pending command to evaluate.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> when dispatch is still allowed; otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> CanDispatchAsync(ProviderCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares and submits the command to the provider, returning the raw provider result.
    /// The state machine claim has already been recorded as sent before this call.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="claim">The active claim holding the fence and owner tokens.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider result, or <see langword="null"/> when the outcome cannot be determined.</returns>
    Task<ContactCenterVoiceProviderResult> ExecuteAsync(
        ProviderCommand command,
        ProviderCommandClaim claim,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects a confirmed success outcome onto any domain entities linked to the command.
    /// </summary>
    /// <param name="command">The confirmed command.</param>
    /// <param name="result">The successful provider result that triggered confirmation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ProjectSuccessAsync(
        ProviderCommand command,
        ContactCenterVoiceProviderResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects a definitive failure outcome onto any domain entities linked to the command.
    /// </summary>
    /// <param name="command">The command that is being compensated.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ProjectFailureAsync(ProviderCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects an unknown outcome onto any domain entities linked to the command.
    /// </summary>
    /// <param name="command">The command whose outcome is unknown.</param>
    /// <param name="errorCode">The provider or internal error code describing the uncertainty.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ProjectOutcomeUnknownAsync(
        ProviderCommand command,
        string errorCode,
        CancellationToken cancellationToken = default);
}
