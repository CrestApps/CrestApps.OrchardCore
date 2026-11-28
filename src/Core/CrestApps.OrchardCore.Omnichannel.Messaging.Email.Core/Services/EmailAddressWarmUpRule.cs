using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// Dates the warm-up of an email address that warms up without a start date, however the address is saved.
/// </summary>
/// <remarks>
/// The warm-up counts its days from its start, and a missing start is read as today, so an address without one would
/// stay on its first day's limit for good. The editor dates the warm-up when it is turned on, but a recipe, a deployment
/// or an import can carry it turned on without a date, so it is dated here, in the step the address's own handler runs
/// for every save once the editor, a recipe or an import has set the address.
/// </remarks>
internal sealed class EmailAddressWarmUpRule : IChannelEndpointRule
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAddressWarmUpRule"/> class.
    /// </summary>
    /// <param name="clock">The clock the warm-up is dated by.</param>
    public EmailAddressWarmUpRule(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task ValidateAsync(ValidatingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task PrepareAsync(OmnichannelChannelEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        DateWarmUp(endpoint, _clock.UtcNow);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts the warm-up of an address that warms up without a start date at <paramref name="utcNow"/>.
    /// </summary>
    /// <param name="endpoint">The address.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns><see langword="true"/> when the warm-up was dated.</returns>
    internal static bool DateWarmUp(OmnichannelChannelEndpoint endpoint, DateTime utcNow)
    {
        if (endpoint is null ||
            !endpoint.TryGet<EmailAddressSettings>(out var settings) ||
            settings.Limits is not { WarmUp: true, WarmUpStartedUtc: null })
        {
            return false;
        }

        // The settings are read as a copy, so the dated copy is written back.
        settings.Limits.WarmUpStartedUtc = utcNow;
        endpoint.Put(settings);

        return true;
    }
}
