using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Telephony.Hubs;

/// <summary>
/// Lets a soft phone say what it can do once it has registered, so the server only uses behavior the client
/// listening on the credential understands.
/// </summary>
public sealed partial class TelephonyHub
{
    // More than any real client reports; a longer list is not a client telling the truth.
    private const int MaximumReportedCapabilities = 16;

    /// <summary>
    /// Records what the caller's soft phone can do, against the browser credential it is registered on. Scoped to the
    /// caller's own credentials and limited to the capabilities the platform knows, so a client can neither mark
    /// someone else's credential nor invent a capability.
    /// </summary>
    /// <param name="credentialId">The provider credential identifier the client registered on.</param>
    /// <param name="capabilities">The capabilities the client reports (see <see cref="TelephonyConstants.SoftPhoneClientCapabilities"/>).</param>
    public async Task ReportCredentialCapabilities(string credentialId, string[] capabilities)
    {
        if (string.IsNullOrEmpty(credentialId))
        {
            return;
        }

        var known = (capabilities ?? [])
            .Take(MaximumReportedCapabilities)
            .Where(capability => TelephonyConstants.SoftPhoneClientCapabilities.All.Contains(capability, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("ReportCredentialCapabilities");
                return;
            }

            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            // Every registrar is offered the id; each only acts on a credential it owns for this user.
            foreach (var registrar in scope.ServiceProvider.GetServices<ISoftPhoneClientCapabilityRegistrar>())
            {
                await registrar.ReportCapabilitiesAsync(userId, credentialId, known, Context.ConnectionAborted);
            }
        });
    }
}
