using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Pushes server-initiated events to a user's soft phones through the telephony hub, in the user's tenant-scoped
/// group.
/// </summary>
public sealed class SoftPhoneClientNotifier : ISoftPhoneClientNotifier
{
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhoneClientNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">The soft-phone hub context.</param>
    /// <param name="shellSettings">The tenant shell settings used to scope hub groups.</param>
    public SoftPhoneClientNotifier(IHubContext<TelephonyHub, ITelephonyClient> hubContext, ShellSettings shellSettings)
    {
        _hubContext = hubContext;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public Task CallStateChangedAsync(string userId, TelephonyCall call)
        => GetUserClients(userId).CallStateChanged(call);

    /// <inheritdoc/>
    public Task IncomingCallAnsweredAsync(string userId, IncomingCallAnsweredNotification notification)
        => GetUserClients(userId).IncomingCallAnswered(notification);

    /// <inheritdoc/>
    public Task DialRequestedAsync(string userId, TelephonyDialRequest request)
        => GetUserClients(userId).DialRequested(request);

    private ITelephonyClient GetUserClients(string userId)
        => _hubContext.Clients.Group(TenantSignalRGroupName.ForUser(_tenantName, userId));
}
