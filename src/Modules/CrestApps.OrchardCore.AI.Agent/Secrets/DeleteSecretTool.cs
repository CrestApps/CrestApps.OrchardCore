using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.AI.Agent.Secrets;

/// <summary>
/// Deletes a secret from its store through the secret manager, as the Delete action of the Secrets admin page does.
/// It never reads the value of the secret.
/// </summary>
public sealed class DeleteSecretTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteSecret";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The name of the secret to delete, exactly as listSecrets returns it."
        },
        "store": {
          "type": "string",
          "description": "The name of the store the secret is in, from listSecrets. It is needed only when the name is in several stores."
        }
      },
      "required": [
        "name"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a secret from its store; this cannot be undone, and every setting that references the secret by name stops working. No lookup tells which settings use a secret, so ask the person where it is used first.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageSecrets))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("name", out var name);
        arguments.TryGetFirstString("store", out var store);

        var manager = services.GetRequiredService<ISecretManager>();
        var (info, error) = await SecretToolSupport.FindAsync(manager, name, store);

        if (error is not null)
        {
            return $"Nothing was deleted: {error}";
        }

        if (!SecretToolSupport.IsWritableStore(manager, info.Store))
        {
            return $"Nothing was deleted: the secret store '{info.Store}' is read-only, so the secret '{info.Name}' cannot be deleted from it here.";
        }

        await manager.RemoveSecretAsync(info.Name, info.Store);

        return AgentToolResults.Json(new
        {
            deleted = true,
            name = info.Name,
            store = info.Store,
            type = SecretToolSupport.GetTypeName(info.Type),
            note = "The secret cannot be restored. Every setting that references it by name (email, SMS, external sign-in, OpenID keys and others) stops working until it names another secret; no lookup tells which settings used it.",
        });
    }
}
