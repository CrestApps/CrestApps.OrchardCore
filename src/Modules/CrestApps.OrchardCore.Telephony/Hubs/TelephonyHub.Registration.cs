using CrestApps.Core.Support;
using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Telephony.Hubs;

/// <summary>
/// Ties each soft phone's credential registration to its connection, so a window that closes stops being where the
/// user's calls go.
/// </summary>
/// <remarks>
/// A user with the soft phone open in several windows has a credential registered per window, and calls go to the one
/// registered last. Closing that window left the server dialing a credential nothing was listening on until another
/// window happened to register again; now its closing connection is reported, and a window still open takes the calls.
/// </remarks>
public sealed partial class TelephonyHub
{
    /// <inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var userId = Context.UserIdentifier;

        if (!string.IsNullOrEmpty(userId))
        {
            try
            {
                await ShellScope.UsingChildScopeAsync(async scope =>
                {
                    // Each registrar only acts on the credentials this user's connection registered on.
                    foreach (var registrar in scope.ServiceProvider.GetServices<ISoftPhoneCredentialRegistrar>())
                    {
                        if (registrar is ISoftPhoneConnectionRegistrar connectionRegistrar)
                        {
                            await connectionRegistrar.ReportConnectionClosedAsync(userId, Context.ConnectionId, HubConnectionWork.MustComplete);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                // A closing connection that cannot be recorded leaves its credential preferred, as before; it must not
                // fail the disconnect.
                _logger.LogWarning(ex, "Could not record that soft-phone connection '{ConnectionId}' closed.", Context.ConnectionId.SanitizeLogValue());
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // The connection that reported the registration travels with it when the registrar can use it.
    private Task<bool> ReportRegisteredOnConnectionAsync(ISoftPhoneCredentialRegistrar registrar, string userId, string credentialId)
        => registrar is ISoftPhoneConnectionRegistrar connectionRegistrar
            ? connectionRegistrar.ReportRegisteredAsync(userId, credentialId, Context.ConnectionId, Context.ConnectionAborted)
            : registrar.ReportRegisteredAsync(userId, credentialId, Context.ConnectionAborted);
}
