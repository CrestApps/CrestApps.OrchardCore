using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes a resolved, provider-safe transfer destination.
/// </summary>
public sealed class TransferDestinationResolutionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the destination is allowed.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the redacted failure reason.
    /// </summary>
    public string FailureReason { get; set; }

    /// <summary>
    /// Gets or sets the destination type.
    /// </summary>
    public InteractionTransferTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the provider-safe resolved destination.
    /// </summary>
    public string ResolvedTarget { get; set; }

    /// <summary>
    /// Creates a successful destination result.
    /// </summary>
    /// <param name="targetType">The destination type.</param>
    /// <param name="resolvedTarget">The provider-safe destination.</param>
    /// <returns>The destination result.</returns>
    public static TransferDestinationResolutionResult Success(
        InteractionTransferTargetType targetType,
        string resolvedTarget)
    {
        return new TransferDestinationResolutionResult
        {
            Succeeded = true,
            TargetType = targetType,
            ResolvedTarget = resolvedTarget,
        };
    }

    /// <summary>
    /// Creates a denied destination result.
    /// </summary>
    /// <returns>The denied destination result.</returns>
    public static TransferDestinationResolutionResult Denied()
    {
        return new TransferDestinationResolutionResult
        {
            Succeeded = false,
            FailureReason = "The requested transfer destination is not available.",
        };
    }
}
