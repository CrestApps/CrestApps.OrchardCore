using CrestApps.Core.Support;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Hubs;

/// <summary>
/// Tells whatever keeps a record of hold time that the agent held or resumed a call.
/// </summary>
/// <remarks>
/// A provider that performs hold in the agent's own media accepts the command without doing anything itself and
/// leaves the browser to swap in the hold audio, so it never reports the hold afterwards. Until this existed a call
/// the agent held for minutes was recorded as all talk: nothing on the server knew the hold had happened.
/// </remarks>
public sealed partial class TelephonyHub
{
    private async Task<TelephonyResult> ExecuteHoldChangeAsync(CallReference call, bool isOnHold)
    {
        var actionName = isOnHold ? "Hold" : "Resume";
        var changedUtc = default(DateTime);

        var result = await ExecuteAsync(
            actionName,
            () => DescribeCallReference(call),
            (service, token) => isOnHold ? service.HoldAsync(call, token) : service.ResumeAsync(call, token),
            () => GetCallIds(call),
            (services, _) =>
            {
                // The moment the agent asked, read before the provider is called: that is when the customer started
                // (or stopped) waiting, however long the provider or the browser then takes.
                changedUtc = services.GetRequiredService<IClock>().UtcNow;

                return Task.FromResult<TelephonyResult>(null);
            });

        if (result?.Succeeded == true && changedUtc != default && !string.IsNullOrEmpty(call?.CallId))
        {
            await ObserveHoldChangeAsync(call.CallId, isOnHold, changedUtc);
        }

        return result;
    }

    private async Task ObserveHoldChangeAsync(string callId, bool isOnHold, DateTime changedUtc)
    {
        var userId = Context.UserIdentifier;

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var observers = scope.ServiceProvider.GetServices<ITelephonyCallHoldObserver>().ToArray();

            if (observers.Length == 0)
            {
                return;
            }

            // The call was authorized against this user's history before the provider was asked, so the history's
            // own record of it -- not anything the client sent -- says which interaction it belongs to.
            var store = scope.ServiceProvider.GetService<ITelephonyInteractionStore>();
            var interaction = store is null
                ? null
                : await store.FindByCallIdAsync(userId, callId, HubConnectionWork.MustComplete);

            var change = new TelephonyCallHoldChange
            {
                CallId = callId,
                ProviderName = interaction?.ProviderName,
                InteractionId = interaction?.InteractionId,
                UserId = userId,
                IsOnHold = isOnHold,
                ChangedUtc = changedUtc,
            };

            foreach (var observer in observers)
            {
                try
                {
                    // The hold is already in effect; recording it must finish even if the agent's connection drops.
                    await observer.CallHoldChangedAsync(change, HubConnectionWork.MustComplete);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        ex,
                        "The hold change on call {CallId} for user {UserId} could not be recorded by {Observer}.",
                        callId.SanitizeLogValue(),
                        RedactedUserId(),
                        observer.GetType().Name.SanitizeLogValue());
                }
            }
        });
    }
}
