using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.OpenId;
using OrchardCore.OpenId.Abstractions.Managers;

namespace CrestApps.OrchardCore.AI.Agent.OpenId;

/// <summary>
/// Deletes an OpenID Connect client application through the application manager, as the Delete action of the
/// Applications admin page does.
/// </summary>
public sealed class DeleteOpenIdApplicationTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteOpenIdApplication";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the application to delete, from listOpenIdApplications."
        },
        "clientId": {
          "type": "string",
          "description": "The client id of that application, exactly as listOpenIdApplications returns it. It is checked against the id so the wrong application is not deleted, and it shows the person who approves which client stops working."
        }
      },
      "required": [
        "id",
        "clientId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes an OpenID application; every client that uses its client id stops working. This cannot be undone.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OpenIdPermissions.ManageApplications))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("id", out var id) || !arguments.TryGetFirstString("clientId", out var clientId))
        {
            return "Nothing was deleted: give both id and clientId of the application. Use listOpenIdApplications to get them.";
        }

        var manager = services.GetRequiredService<IOpenIdApplicationManager>();
        var (application, error) = await OpenIdToolSupport.FindAsync(manager, id, clientId, cancellationToken);

        if (error is not null)
        {
            return $"Nothing was deleted: {error}";
        }

        var state = await OpenIdToolSupport.ReadAsync(manager, application, cancellationToken);

        await manager.DeleteAsync(application, cancellationToken);

        return AgentToolResults.Json(new
        {
            deleted = true,
            id = state.Id,
            clientId = state.ClientId,
            displayName = state.DisplayName,
            note = "The application cannot be restored. Clients that use this client id can no longer sign in or get tokens.",
        });
    }
}
