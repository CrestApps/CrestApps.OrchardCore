using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.AI;

/// <summary>
/// Lists the broadcasts newest first with their counts, as the Broadcasts page does. The recipients are counted, never
/// listed.
/// </summary>
internal sealed class ListMessagingBroadcastsTool : MessagingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listMessagingBroadcasts";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many broadcasts a page holds, at most 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists broadcasts with status and sent, failed and skipped counts.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => MessagingPermissions.SendGroupMessages;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (page, pageSize) = ReadPaging(arguments);
        var broadcasts = (await services.GetRequiredService<IMessagingBroadcastManager>().GetAllAsync(cancellationToken))
            .OrderByDescending(broadcast => broadcast.CreatedUtc)
            .ToList();
        var localClock = services.GetService<ILocalClock>();
        var items = new List<object>();

        foreach (var broadcast in broadcasts.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(await DescribeAsync(localClock, broadcast));
        }

        return Json(new
        {
            items,
            total = broadcasts.Count,
            page,
            pageSize,
            truncated = page * pageSize < broadcasts.Count,
        });
    }

    /// <summary>
    /// Describes a broadcast from an explicit list of fields.
    /// </summary>
    internal static async Task<object> DescribeAsync(ILocalClock localClock, MessagingBroadcast broadcast)
        => new
        {
            id = broadcast.ItemId,
            name = broadcast.Name,
            channel = broadcast.Channel,
            fromAddress = broadcast.ServiceAddress,
            status = broadcast.Status.ToString(),
            recipientCount = broadcast.Recipients?.Count ?? 0,
            sentCount = broadcast.SentCount,
            failedCount = broadcast.FailedCount,
            skippedCount = broadcast.SkippedCount,
            created = await ToLocalAsync(localClock, broadcast.CreatedUtc),
            completed = await ToLocalAsync(localClock, broadcast.CompletedUtc),
        };
}
