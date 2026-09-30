using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telephony.Hubs;

/// <summary>
/// Presents the agent's own line on the calls they place from the soft phone.
/// </summary>
public sealed partial class TelephonyHub
{
    // Presents the agent's line on a number dialed from the keypad.
    private async Task<TelephonyResult> StampOutboundLineAsync(IServiceProvider services, DialRequest request, CancellationToken cancellationToken)
    {
        if (request is not null)
        {
            request.From = await ResolveOutboundCallerIdAsync(services, "Dial", cancellationToken);
        }

        return null;
    }

    // Presents the agent's line and name on a colleague's extension.
    private async Task<TelephonyResult> StampExtensionCallerAsync(IServiceProvider services, ExtensionDialRequest request, CancellationToken cancellationToken)
    {
        if (request is not null)
        {
            request.From = await ResolveOutboundCallerIdAsync(services, "DialExtension", cancellationToken);
        }

        return await StampCallerDisplayNameAsync(services, request, cancellationToken);
    }

    // Resolves the caller ID this agent dials out with. It is always the server's answer: a number the browser sent
    // is discarded, so an agent cannot present a line that was not assigned to them. A line that cannot be read
    // falls back to the provider default rather than refusing the call.
    private async Task<string> ResolveOutboundCallerIdAsync(IServiceProvider services, string actionName, CancellationToken cancellationToken)
    {
        var userId = Context.UserIdentifier;
        var resolver = services.GetService<IOutboundLineResolver>();

        if (string.IsNullOrEmpty(userId) || resolver is null)
        {
            return null;
        }

        try
        {
            var line = await resolver.ResolveAsync(userId, cancellationToken);

            if (line is null || string.IsNullOrWhiteSpace(line.Number))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Telephony hub action {Action} for user {UserId} presents the provider default caller ID because the user has no outbound line.",
                        actionName,
                        RedactedUserId());
                }

                return null;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Telephony hub action {Action} for user {UserId} presents outbound line {LineId} ({LineNumber}).",
                    actionName,
                    RedactedUserId(),
                    line.Id,
                    line.Number);
            }

            return line.Number;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Telephony hub action {Action} for user {UserId} could not read the user's outbound line; the provider default caller ID is presented instead.",
                actionName,
                RedactedUserId());

            return null;
        }
    }
}
