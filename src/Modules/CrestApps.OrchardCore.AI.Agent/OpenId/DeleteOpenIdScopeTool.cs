using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OrchardCore.OpenId;
using OrchardCore.OpenId.Abstractions.Managers;

namespace CrestApps.OrchardCore.AI.Agent.OpenId;

/// <summary>
/// Deletes an OpenID Connect scope through the scope manager, as the Delete action of the Scopes admin page does, and
/// names the applications that were allowed to request it.
/// </summary>
public sealed class DeleteOpenIdScopeTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteOpenIdScope";

    // The most applications the result names.
    private const int MaxApplications = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the scope to delete, from listOpenIdScopes."
        },
        "name": {
          "type": "string",
          "description": "The name of that scope, exactly as listOpenIdScopes returns it. It is checked against the id so the wrong scope is not deleted, and it shows the person who approves which scope goes."
        }
      },
      "required": [
        "id",
        "name"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes an OpenID scope; applications that request it will be refused. This cannot be undone.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, OpenIdPermissions.ManageScopes))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("id", out var id) || !arguments.TryGetFirstString("name", out var name))
        {
            return "Nothing was deleted: give both id and name of the scope. Use listOpenIdScopes to get them.";
        }

        var manager = services.GetRequiredService<IOpenIdScopeManager>();
        var scope = await manager.FindByPhysicalIdAsync(id.Trim(), cancellationToken);

        if (scope is null)
        {
            return $"Nothing was deleted: there is no OpenID scope with the id '{id}'. Use listOpenIdScopes to find it.";
        }

        var storedName = await manager.GetNameAsync(scope, cancellationToken);

        if (!string.Equals(storedName, name.Trim(), StringComparison.Ordinal))
        {
            return $"Nothing was deleted: the scope with the id '{id}' is not named '{name}'. Use listOpenIdScopes to get both.";
        }

        // Read before the scope goes, so the person learns which clients lose it. A site registers few applications.
        var applicationManager = services.GetService<IOpenIdApplicationManager>();
        var applications = new List<string>();

        if (applicationManager is not null)
        {
            await foreach (var application in applicationManager.ListAsync(null, null, cancellationToken))
            {
                if (applications.Count < MaxApplications &&
                    await applicationManager.HasPermissionAsync(application, OpenIddictConstants.Permissions.Prefixes.Scope + storedName, cancellationToken))
                {
                    applications.Add(await applicationManager.GetClientIdAsync(application, cancellationToken));
                }
            }
        }

        await manager.DeleteAsync(scope, cancellationToken);

        return AgentToolResults.Json(new
        {
            deleted = true,
            id,
            name = storedName,
            applicationsThatWereAllowedIt = applications,
            note = "The scope cannot be restored. A request for it is refused from now on; the applications listed still name it in their scopes.",
        });
    }
}
