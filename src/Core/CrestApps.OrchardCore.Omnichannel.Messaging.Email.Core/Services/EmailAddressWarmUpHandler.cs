using CrestApps.Core;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// Dates the warm-up of an email address that warms up without a start date, however the address is saved.
/// </summary>
/// <remarks>
/// The warm-up counts its days from its start, and a missing start is read as today, so an address without one would
/// stay on its first day's limit for good. The editor dates the warm-up when it is turned on, but a recipe, a deployment
/// or an import can carry it turned on without a date, so it is dated here, where every save of an address passes. The
/// Email feature depends on the address list, so this handler runs after the address's own handler has applied what a
/// recipe carries.
/// </remarks>
internal sealed class EmailAddressWarmUpHandler : CatalogEntryHandlerBase<OmnichannelChannelEndpoint>
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAddressWarmUpHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock the warm-up is dated by.</param>
    public EmailAddressWarmUpHandler(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
    {
        DateWarmUp(context.Model, _clock.UtcNow);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<OmnichannelChannelEndpoint> context, CancellationToken cancellationToken = default)
    {
        DateWarmUp(context.Model, _clock.UtcNow);

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
