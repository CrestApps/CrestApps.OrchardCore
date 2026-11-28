using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataPipelines.SharedLinks;

namespace CrestApps.OrchardCore.AI.Agent.SharedLinks;

/// <summary>
/// Revokes a shared link as the Revoke action of the Shared Links admin page does, through
/// <see cref="ISharedLinkManager.RevokeAsync"/>: the link stops working for all its recipients and its resource is kept
/// until the link is deleted.
/// </summary>
public sealed class RevokeSharedLinkTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "revokeSharedLink";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "linkId": {
          "type": "string",
          "description": "The id of the link, from listSharedLinks."
        }
      },
      "required": [
        "linkId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Stops a shared link from working for all its recipients. The shared file or resource is kept until the link is deleted.";

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

        var linkId = arguments.GetFirstValueOrDefault<string>("linkId")?.Trim();

        if (string.IsNullOrEmpty(linkId))
        {
            return "Nothing was revoked: give 'linkId', the id of the link from listSharedLinks.";
        }

        var manager = services.GetRequiredService<ISharedLinkManager>();
        var link = await manager.GetAsync(linkId, cancellationToken);

        if (link is null)
        {
            return $"Nothing was revoked: there is no shared link '{linkId}'. Call listSharedLinks to find its id.";
        }

        if (link.RevokedUtc is not null)
        {
            return AgentToolResults.Json(new
            {
                revoked = false,
                message = $"The link to '{link.Title}' was already revoked; nothing changed.",
                link = await SharedLinkToolSupport.DescribeAsync(services, link),
            });
        }

        await manager.RevokeAsync(link, cancellationToken);

        return AgentToolResults.Json(new
        {
            revoked = true,
            message = $"The link to '{link.Title}' no longer works. Its resource is kept until the link is deleted.",
            link = await SharedLinkToolSupport.DescribeAsync(services, link),
        });
    }
}
