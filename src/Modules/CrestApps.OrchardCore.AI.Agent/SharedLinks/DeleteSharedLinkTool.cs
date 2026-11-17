using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataPipelines.SharedLinks;

namespace CrestApps.OrchardCore.AI.Agent.SharedLinks;

/// <summary>
/// Deletes a shared link as the Delete action of the Shared Links admin page does, through
/// <see cref="ISharedLinkManager.DeleteAsync"/>, which lets the module that shared it release the resource, such as the
/// stored copy of a file.
/// </summary>
public sealed class DeleteSharedLinkTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteSharedLink";

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
    public override string Description => "Deletes a shared link together with the resource it shared, such as the stored copy of an exported file. Cannot be undone; use revokeSharedLink to only stop the link from working.";

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
            return "Nothing was deleted: give 'linkId', the id of the link from listSharedLinks.";
        }

        var manager = services.GetRequiredService<ISharedLinkManager>();
        var link = await manager.GetAsync(linkId, cancellationToken);

        if (link is null)
        {
            return $"Nothing was deleted: there is no shared link '{linkId}'. Call listSharedLinks to find its id.";
        }

        // Describe the link before it is deleted, so the result says what went.
        var description = await SharedLinkToolSupport.DescribeAsync(services, link);

        await manager.DeleteAsync(link, cancellationToken);

        return AgentToolResults.Json(new
        {
            deleted = true,
            message = $"The link to '{link.Title}' and the resource it shared were deleted.",
            link = description,
        });
    }
}
