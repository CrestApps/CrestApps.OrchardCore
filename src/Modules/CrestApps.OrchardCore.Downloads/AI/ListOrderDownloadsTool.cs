using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Downloads.AI;

/// <summary>
/// Lists an order's download entitlements as the Downloads card of the order's admin page does
/// (<see cref="DownloadService.ListAsync"/>), with why each link stopped working (<see cref="DownloadService.GetProblem"/>).
/// The download link itself is a bearer link to the file and is never returned.
/// </summary>
internal sealed class ListOrderDownloadsTool : DownloadsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listOrderDownloads";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "orderId": {
          "type": "string",
          "description": "The order's id."
        },
        "orderNumber": {
          "type": "string",
          "description": "The order's number, when the id is not known."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the download entitlements of an order: file, downloads used and allowed, expiry, and whether the link is working, revoked, expired or over its limit. Use it when a buyer says a download link does not work.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => DownloadsPermissions.ManageDownloads;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var orderId = ReadString(arguments, "orderId");
        var orderNumber = ReadString(arguments, "orderNumber");

        if (orderId is null && orderNumber is null)
        {
            return "Give the order's id or its number.";
        }

        if (orderId is null)
        {
            var order = await services.GetRequiredService<IOrderStore>().FindByNumberAsync(orderNumber, cancellationToken);

            if (order is null)
            {
                return $"No order has the number '{orderNumber}'.";
            }

            orderId = order.ItemId;
        }

        var entitlements = await services.GetRequiredService<DownloadService>().ListAsync(orderId, cancellationToken);
        var now = services.GetRequiredService<IClock>().UtcNow;
        var items = new List<object>();

        foreach (var entitlement in entitlements)
        {
            items.Add(await DescribeAsync(services, entitlement, now));
        }

        return Json(new
        {
            orderId,
            orderNumber = entitlements.Count > 0 ? entitlements[0].OrderNumber : orderNumber,
            items,
            total = items.Count,
            message = items.Count == 0 ? "This order has no downloads: it bought no digital files, or it is not paid yet." : null,
        });
    }

    /// <summary>
    /// Describes an entitlement from an explicit list of fields, with why its link does not work.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="entitlement">The entitlement.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns>The entitlement's fields.</returns>
    internal static async Task<object> DescribeAsync(IServiceProvider services, DownloadEntitlement entitlement, DateTime utcNow)
        => new
        {
            id = entitlement.ItemId,
            orderId = entitlement.OrderId,
            orderNumber = entitlement.OrderNumber,
            productId = entitlement.ProductId,
            product = entitlement.ProductTitle,
            fileName = entitlement.FileName,
            downloadsUsed = entitlement.DownloadCount,
            downloadLimit = entitlement.DownloadLimit,
            remaining = entitlement.Remaining,
            expiresAt = await LocalTimeAsync(services, entitlement.ExpiresUtc),
            lastDownloadedAt = await LocalTimeAsync(services, entitlement.LastDownloadedUtc),
            revokedAt = await LocalTimeAsync(services, entitlement.RevokedUtc),
            revokeReason = entitlement.RevokeReason,
            linkState = DescribeProblem(DownloadService.GetProblem(entitlement, utcNow)),
        };

    private static string DescribeProblem(DownloadProblem problem)
        => problem switch
        {
            DownloadProblem.None => "working",
            DownloadProblem.Revoked => "revoked",
            DownloadProblem.Expired => "expired",
            DownloadProblem.LimitReached => "overLimit",
            _ => "notFound",
        };
}
