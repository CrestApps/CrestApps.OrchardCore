using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataPipelines.SharedLinks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.SharedLinks;

/// <summary>
/// Lists the shared links of every module, the most recent first, as the Shared Links admin page does, through
/// <see cref="ISharedLinkManager"/>. A link's URL holds a token that is never stored, so no tool can return it.
/// </summary>
public sealed class ListSharedLinksTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listSharedLinks";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "query": {
          "type": "string",
          "description": "Only list the links whose title, or the name of what shared them (such as a pipeline or a report), contains this text."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of links per page, 20 by default and 50 at most."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the shared links of every module, the most recent first, with their id, title, kind, what shared them, recipients, expiry, uses, and whether they still work (Active, Expired, UsedUp or Revoked); optional text filter and paging. It never returns the link's secret URL.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageSharedLinks))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var query = arguments.GetFirstValueOrDefault<string>("query")?.Trim();
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var (links, count) = await services.GetRequiredService<ISharedLinkManager>()
            .ListAsync(string.IsNullOrEmpty(query) ? null : query, (page - 1) * pageSize, pageSize, cancellationToken);

        var items = new List<object>();

        foreach (var link in links)
        {
            items.Add(await SharedLinkToolSupport.DescribeAsync(services, link));
        }

        return AgentToolResults.Json(new
        {
            items,
            total = count,
            page,
            pageSize,
            truncated = count > page * pageSize,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(services.GetRequiredService<ILocalClock>()),
            adminUrl = SharedLinkToolSupport.GetAdminPath(services),
        });
    }
}
