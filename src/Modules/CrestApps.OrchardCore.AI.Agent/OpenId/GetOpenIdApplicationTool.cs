using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.OpenId;
using OrchardCore.OpenId.Abstractions.Managers;

namespace CrestApps.OrchardCore.AI.Agent.OpenId;

/// <summary>
/// Gets one OpenID Connect client application through the application manager, with the fields the Edit page of the
/// Applications admin area shows. The client secret is not among them.
/// </summary>
public sealed class GetOpenIdApplicationTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getOpenIdApplication";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the application, from listOpenIdApplications. Give id or clientId."
        },
        "clientId": {
          "type": "string",
          "description": "The client id of the application. Give id or clientId."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Gets one OpenID application by id or client id: type, consent type, redirect URIs, allowed flows and endpoints, PKCE and PAR requirements, roles and scopes. It never returns the client secret. Call it before saveOpenIdApplication.";

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

        arguments.TryGetFirstString("id", out var id);
        arguments.TryGetFirstString("clientId", out var clientId);

        var manager = services.GetRequiredService<IOpenIdApplicationManager>();
        var (application, error) = await OpenIdToolSupport.FindAsync(manager, id, clientId, cancellationToken);

        if (error is not null)
        {
            return $"Nothing was found: {error}";
        }

        var state = await OpenIdToolSupport.ReadAsync(manager, application, cancellationToken);

        return AgentToolResults.Json(new
        {
            application = OpenIdToolSupport.Describe(services, state),
            note = "The client secret is never returned. A person sets or rotates it on the Applications admin page.",
        });
    }
}
