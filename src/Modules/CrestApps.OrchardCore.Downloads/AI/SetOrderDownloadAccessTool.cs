using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Downloads.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Downloads.AI;

/// <summary>
/// Revokes or resets one download entitlement, as Revoke and Reset on the Downloads card of an order's admin page do:
/// <see cref="DownloadService.RevokeAsync"/> with "Taken back by" the caller (and the reason given), or
/// <see cref="DownloadService.ResetAsync"/> (the count back to zero, the revoke cleared and a fresh expiry from the
/// download settings). The two undo each other, so the tool does not ask for approval.
/// </summary>
internal sealed class SetOrderDownloadAccessTool : DownloadsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setOrderDownloadAccess";

    private const int MaxReasonLength = 500;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entitlementId": {
          "type": "string",
          "description": "The download entitlement's id, from listOrderDownloads."
        },
        "action": {
          "type": "string",
          "enum": ["revoke", "reset"],
          "description": "revoke stops the buyer downloading the file; reset lets them download again with the count at zero and a new expiry."
        },
        "reason": {
          "type": "string",
          "description": "For revoke only: why the download is taken back, kept with the entitlement."
        }
      },
      "required": ["entitlementId", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Revokes one download entitlement so the buyer can no longer download the file, or resets it so the buyer can download again with the count at zero and a new expiry. Takes the entitlement id from listOrderDownloads.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => DownloadsPermissions.ManageDownloads;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "entitlementId") is not { } entitlementId)
        {
            return "Nothing was changed: give the entitlement's id. Use listOrderDownloads to find it.";
        }

        var action = ReadString(arguments, "action");
        var revoke = string.Equals(action, "revoke", StringComparison.OrdinalIgnoreCase);

        if (!revoke && !string.Equals(action, "reset", StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was changed: action must be revoke or reset.";
        }

        var reason = ReadString(arguments, "reason");

        if (!revoke && reason is not null)
        {
            return "Nothing was changed: a reason is kept only when a download is revoked. Leave it out to reset.";
        }

        if (reason?.Length > MaxReasonLength)
        {
            return $"Nothing was changed: the reason can be up to {MaxReasonLength} characters.";
        }

        var downloads = services.GetRequiredService<DownloadService>();
        var entitlement = await downloads.FindAsync(entitlementId, cancellationToken);

        if (entitlement is null)
        {
            return $"Nothing was changed: no download entitlement has the id '{entitlementId}'. Use listOrderDownloads to find it.";
        }

        if (revoke)
        {
            var actor = caller.Identity?.Name;

            await downloads.RevokeAsync(entitlement, reason is null ? $"Taken back by {actor}." : $"Taken back by {actor}: {reason}", cancellationToken);
        }
        else
        {
            await downloads.ResetAsync(entitlement, cancellationToken);
        }

        if (await CommitAsync(services, cancellationToken) is { } error)
        {
            return error;
        }

        return Json(new
        {
            entitlement = await ListOrderDownloadsTool.DescribeAsync(services, entitlement, services.GetRequiredService<IClock>().UtcNow),
            message = revoke
                ? $"The buyer can no longer download {entitlement.FileName}. Reset it to give the download back."
                : $"The buyer can download {entitlement.FileName} again.",
        });
    }
}
