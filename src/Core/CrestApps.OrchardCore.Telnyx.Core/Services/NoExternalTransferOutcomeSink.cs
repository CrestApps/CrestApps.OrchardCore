using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The transfer-outcome sink for a tenant with no Contact Center. It claims nothing.
/// </summary>
/// <remarks>
/// The webhook service is part of the base Telnyx feature and is constructed on tenants that have never enabled
/// Contact Center, so it cannot depend on a service only Contact Center registers.
/// </remarks>
public sealed class NoExternalTransferOutcomeSink : IExternalTransferOutcomeSink
{
    /// <inheritdoc/>
    public Task<bool> HandleAsync(ExternalTransferOutcome outcome, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
