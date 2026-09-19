using CrestApps.Core.Hosting;
using CrestApps.Core.SignalR;
using CrestApps.Core.Telephony.Models;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.Core.Telephony.Services;

/// <summary>
/// Pushes call events to a user's soft phones over a SignalR hub, scoping every push to the tenant the work
/// belongs to so two tenants that happen to use the same user identifier never reach each other's phones.
/// </summary>
/// <typeparam name="THub">
/// The host's concrete hub. The hub carries the host's authorization, which is why the framework never
/// declares it and takes it as a type argument instead.
/// </typeparam>
public sealed class TelephonySoftPhoneNotifier<THub> : ITelephonySoftPhoneNotifier
    where THub : Hub<ITelephonyClient>
{
    private readonly IHubContext<THub, ITelephonyClient> _hubContext;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonySoftPhoneNotifier{THub}"/> class.
    /// </summary>
    /// <param name="hubContext">The hub context used to push events to connected clients.</param>
    /// <param name="tenantAccessor">Names the tenant the pushes are scoped to.</param>
    public TelephonySoftPhoneNotifier(
        IHubContext<THub, ITelephonyClient> hubContext,
        ITenantAccessor tenantAccessor)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(tenantAccessor);

        _hubContext = hubContext;
        _tenantName = tenantAccessor.TenantName;
    }

    /// <inheritdoc/>
    public Task NotifyIncomingCallAsync(string userId, TelephonyCall call, IncomingCallContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return Group(userId).IncomingCall(call, context);
    }

    /// <inheritdoc/>
    public Task NotifyCallStateChangedAsync(string userId, TelephonyCall call, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return Group(userId).CallStateChanged(call);
    }

    /// <inheritdoc/>
    public Task RequestDialAsync(string userId, TelephonyDialRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return Group(userId).DialRequested(request);
    }

    private ITelephonyClient Group(string userId)
        => _hubContext.Clients.Group(TenantSignalRGroupName.ForUser(_tenantName, userId));
}
