using CrestApps.Core.Support;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Pushes where the other party of a call stands to the soft phones of the user the call belongs to, found through the
/// call's history entry.
/// </summary>
public sealed class TelephonyRemotePartyNotifier : ITelephonyRemotePartyNotifier
{
    private readonly ITelephonyInteractionStore _telephonyInteractionStore;
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;
    private readonly ILogger _logger;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyRemotePartyNotifier"/> class.
    /// </summary>
    /// <param name="telephonyInteractionStore">The call history, which names the user a call belongs to.</param>
    /// <param name="hubContext">The soft-phone hub context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="shellSettings">The tenant shell settings used to scope hub groups.</param>
    public TelephonyRemotePartyNotifier(
        ITelephonyInteractionStore telephonyInteractionStore,
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        ILogger<TelephonyRemotePartyNotifier> logger,
        ShellSettings shellSettings)
    {
        _telephonyInteractionStore = telephonyInteractionStore;
        _hubContext = hubContext;
        _logger = logger;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public async Task NotifyAsync(string providerName, string providerCallId, RemotePartyState state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(providerCallId))
        {
            return;
        }

        var interaction = await _telephonyInteractionStore.FindByProviderCallIdAsync(providerName, providerCallId.Trim(), cancellationToken);

        if (interaction is null || string.IsNullOrWhiteSpace(interaction.UserId))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The other party of provider {ProviderName} call {CallId} is {State}, but the call has no history entry naming its user; no soft phone was told.",
                    providerName,
                    providerCallId.SanitizeLogValue(),
                    state);
            }

            return;
        }

        await _hubContext.Clients
            .Group(TenantSignalRGroupName.ForUser(_tenantName, interaction.UserId))
            .RemotePartyChanged(new TelephonyRemotePartyUpdate
            {
                CallId = interaction.CallId,
                State = state,
            });

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Told the soft phone of user {UserId} that the other party of call {CallId} is {State}.",
                interaction.UserId.SanitizeLogValue(),
                interaction.CallId.SanitizeLogValue(),
                state);
        }
    }
}
