using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// Processes inbound emails already committed to the durable inbox on fresh shell scopes of their own, after the caller
/// (a webhook answering its provider, the mailbox reader moving on to the next email) has finished. Each email gets its
/// own scope, so one conversation's AI reply, which waits a while before answering, never holds up another's. Should a
/// scope fail or the node stop, the inbox's own background task dispatches the stored email again.
/// </summary>
public static class EmailInboxBackgroundDispatch
{
    /// <summary>
    /// Dispatches the stored emails in the background.
    /// </summary>
    /// <param name="shellHost">The shell host the scopes are created from.</param>
    /// <param name="shellSettings">The tenant's shell settings.</param>
    /// <param name="inboxMessageIds">The inbox records of the emails.</param>
    /// <returns>A task that completes when every email has been handed to its scope, not when it has been processed.</returns>
    public static async Task DispatchAsync(IShellHost shellHost, ShellSettings shellSettings, IEnumerable<string> inboxMessageIds)
    {
        ArgumentNullException.ThrowIfNull(shellHost);
        ArgumentNullException.ThrowIfNull(shellSettings);

        foreach (var id in inboxMessageIds ?? [])
        {
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var scope = await shellHost.GetScopeAsync(shellSettings);

            _ = scope.UsingAsync(async shellScope =>
            {
                var inbox = shellScope.ServiceProvider.GetService<IProviderWebhookInbox>();

                if (inbox is null)
                {
                    return;
                }

                try
                {
                    await inbox.DispatchAsync(id);
                }
                catch (Exception ex)
                {
                    shellScope.ServiceProvider.GetRequiredService<ILogger<EmailInboundReceiver>>()
                        .LogError(ex, "Processing the inbound email {InboxMessageId} failed; the inbox retries it.", id.SanitizeLogValue());
                }
            });
        }
    }
}
