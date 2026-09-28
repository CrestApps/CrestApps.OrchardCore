using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The digits sink for a tenant with no Contact Center. It claims nothing, so a key press on a Telnyx call is
/// left to whatever else on the tenant might care about it.
/// </summary>
/// <remarks>
/// The webhook service is part of the base Telnyx feature and is constructed on tenants that have never enabled
/// Contact Center, so it cannot depend on a service only Contact Center registers.
/// </remarks>
public sealed class NoInboundVoiceDigitsSink : IInboundVoiceDigitsSink
{
    /// <inheritdoc/>
    public Task<bool> HandleDigitsAsync(InboundVoiceDigitsEvent digitsEvent, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
