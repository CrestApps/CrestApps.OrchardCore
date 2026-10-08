using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="IOutboundCallScreeningService"/> implementation. It evaluates every registered
/// <see cref="IOutboundCallScreener"/> and fails closed on the first denial. When no screener is
/// registered — for example, when telephony is used without a compliance module — the origination is
/// permitted so standalone telephony keeps working.
/// </summary>
public sealed class DefaultOutboundCallScreeningService : IOutboundCallScreeningService
{
    private readonly IEnumerable<IOutboundCallScreener> _screeners;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultOutboundCallScreeningService"/> class.
    /// </summary>
    /// <param name="screeners">The registered outbound call screeners, if any.</param>
    public DefaultOutboundCallScreeningService(IEnumerable<IOutboundCallScreener> screeners)
    {
        _screeners = screeners;
    }

    /// <inheritdoc/>
    public async Task<OutboundCallScreeningResult> ScreenAsync(OutboundCallScreeningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var screener in _screeners)
        {
            var result = await screener.ScreenAsync(context, cancellationToken);

            if (result is null)
            {
                // A registered screener that returns no verdict has failed to reach a compliance decision.
                // Treating that silence as approval would reopen the very origination bypass this gate closes,
                // so the origination fails closed instead.
                return OutboundCallScreeningResult.Deny(
                    "ScreeningError",
                    "An outbound call screener did not return a screening decision.");
            }

            if (!result.IsAllowed)
            {
                return result;
            }
        }

        return OutboundCallScreeningResult.Allow();
    }
}
