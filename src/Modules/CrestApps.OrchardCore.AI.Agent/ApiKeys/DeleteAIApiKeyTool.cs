using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ApiKeys;

/// <summary>
/// Deletes (revokes) API keys, as the Delete action of the API keys page does. Clients using a deleted key stop
/// working at once.
/// </summary>
public sealed class DeleteAIApiKeyTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIApiKey";

    private const int MaxKeys = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "apiKeyIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the keys to delete, from listAIApiKeys."
        }
      },
      "required": ["apiKeyIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes (revokes) API keys by id. Clients using them stop working at once. It cannot be undone; to stop a key for a while, disable it with saveAIApiKey instead.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIApiKeys))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "apiKeyIds", out var keyIds) || keyIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the keys to delete in apiKeyIds.";
        }

        if (keyIds.Count > MaxKeys)
        {
            return $"Nothing was deleted: at most {MaxKeys} keys can be deleted at once.";
        }

        var store = services.GetRequiredService<IAIApiKeyStore>();
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var keyId in keyIds)
        {
            var key = await store.FindByIdAsync(keyId, cancellationToken);

            if (key is null)
            {
                skipped.Add(new { id = keyId, reason = "No API key has this ID." });

                continue;
            }

            if (!await store.DeleteAsync(key, cancellationToken))
            {
                skipped.Add(new { id = key.ItemId, name = key.Name, reason = "The key store did not delete it." });

                continue;
            }

            deleted.Add(new { id = key.ItemId, name = key.Name, kind = key.Kind.ToString(), prefix = key.Prefix });
        }

        return AgentToolResults.Json(new { deleted, skipped });
    }
}
