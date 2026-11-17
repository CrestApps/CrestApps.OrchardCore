using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.AI.Agent.Secrets;

/// <summary>
/// Moves one secret, or every secret, from one writable secret store to another through the store operations service
/// the Secret Stores admin page uses, with the checks of that page. The service copies, verifies and then removes
/// each secret; the tool gets only the outcome of each, never a value.
/// </summary>
public sealed class MoveSecretsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "moveSecrets";

    // The most outcomes one result lists.
    private const int MaxResults = 200;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "sourceStore": {
          "type": "string",
          "description": "The name of the writable store the secrets are in now, from listSecrets."
        },
        "destinationStore": {
          "type": "string",
          "description": "The name of another writable store, from listSecrets, the secrets are moved to."
        },
        "name": {
          "type": "string",
          "description": "The name of the one secret to move. Leave it out to move every secret of the source store."
        }
      },
      "required": [
        "sourceStore",
        "destinationStore"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Moves one secret, or all secrets, from one writable secret store to another, for example from the database store to Azure Key Vault, and reports the result per secret. A secret keeps its name, so the settings that reference it keep working.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageSecrets))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("sourceStore", out var sourceStore);
        arguments.TryGetFirstString("destinationStore", out var destinationStore);
        arguments.TryGetFirstString("name", out var name);

        var manager = services.GetRequiredService<ISecretManager>();
        var writableStores = manager.GetStores().Where(store => !store.IsReadOnly).ToList();
        var available = $"The writable stores are: {string.Join(", ", writableStores.Select(store => store.Name))}.";

        // The checks of the Secret Stores page.
        var source = writableStores.FirstOrDefault(store => string.Equals(store.Name, sourceStore?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            return $"Nothing was moved: sourceStore must be an available writable store. {available}";
        }

        var destination = writableStores.FirstOrDefault(store => store != source && string.Equals(store.Name, destinationStore?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (destination is null)
        {
            return $"Nothing was moved: destinationStore must be a different writable store. {available}";
        }

        var operations = services.GetRequiredService<ISecretStoreOperations>();
        IReadOnlyList<SecretStoreOperationResult> results;

        try
        {
            results = string.IsNullOrWhiteSpace(name)
                ? await operations.MoveAllSecretsAsync(source.Name, destination.Name)
                : [await operations.MoveSecretAsync(name.Trim(), source.Name, destination.Name)];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // As on the admin page, what a store answered is not logged or returned: it can hold what the store keeps.
            var logger = services.GetService<ILogger<MoveSecretsTool>>();

            if (logger is not null && logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError("Moving secrets between stores failed ({ExceptionType}).", exception.GetType().Name);
            }

            return "The store operation could not be completed. Check that both stores are available, the permissions of the site on them, and whether another operation is in progress. Use listSecrets to see where each secret is now.";
        }

        var moved = results.Count(result => result.Succeeded);

        return AgentToolResults.Json(new
        {
            sourceStore = source.Name,
            destinationStore = destination.Name,
            movedCount = moved,
            failedCount = results.Count - moved,
            results = results
                .Take(MaxResults)
                .Select(result => new
                {
                    name = result.Name,
                    moved = result.Succeeded,
                    savedInDestination = result.DestinationSaved,
                    failure = result.Failure == SecretStoreOperationFailure.None
                        ? null
                        : result.Failure.ToString(),
                })
                .ToList(),
            truncated = results.Count > MaxResults
                ? true
                : (bool?)null,
            note = results.Count == moved
                ? "Every secret was copied, checked and removed from the source store. The settings that reference a secret by name keep working."
                : "Some secrets were not moved. A secret that was saved in the destination but not removed from the source is in both stores; review each result before trying again.",
        });
    }
}
