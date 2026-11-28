using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.ToolInstances;

/// <summary>
/// Creates a tool instance from a source, or updates one, through the catalog manager the Tool Instances admin page
/// uses. It never takes a credential: it saves everything else and returns the admin page where a person enters one.
/// An instance the running assistant uses cannot be changed, and an address an instance with a stored credential sends
/// it to cannot be changed from a chat.
/// </summary>
public sealed class SaveAIToolInstanceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIToolInstance";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "instanceId": {
          "type": "string",
          "description": "The ID of the tool instance to change, from listAIToolInstances. Leave it out to create one."
        },
        "source": {
          "type": "string",
          "description": "The source of a new tool instance, from listAIToolInstances with includeSources, for example http-api-request or sitemap-documentation. Ignored on an update."
        },
        "name": {
          "type": "string",
          "description": "The unique name of a new tool instance. It cannot change after creation."
        },
        "description": {
          "type": "string",
          "description": "What the tool does, written for the AI model that decides when to call it. Required."
        },
        "settings": {
          "type": "object",
          "description": "The source's settings to set, by the names listAIToolInstances with includeSources gives, for example { \"baseUrl\": \"https://api.example.com\", \"httpMethod\": \"GET\" }. Settings left out keep their value. Credentials and headers are not accepted.",
          "additionalProperties": true
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a tool instance from a source or updates one by id. An instance can call an outside address with stored credentials, so check the address with the user. The name cannot change after creation. An instance attached to the running assistant cannot be changed. Credentials are entered by a person on the admin page this tool returns.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIToolInstances))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadJsonObject(arguments, "settings", out var settings))
        {
            return "Nothing was saved: settings must be an object of setting names and values.";
        }

        arguments.TryGetFirstString("instanceId", out var instanceId);
        arguments.TryGetFirstString("description", out var description);

        var manager = services.GetRequiredService<ISourceCatalogManager<AIToolInstance>>();

        return string.IsNullOrWhiteSpace(instanceId)
            ? await CreateAsync(services, manager, arguments, description, settings, cancellationToken)
            : await UpdateAsync(services, manager, instanceId.Trim(), description, settings, cancellationToken);
    }

    private static async Task<string> CreateAsync(
        IServiceProvider services,
        ISourceCatalogManager<AIToolInstance> manager,
        AIFunctionArguments arguments,
        string description,
        JsonObject settings,
        CancellationToken cancellationToken)
    {
        arguments.TryGetFirstString("source", out var source);
        arguments.TryGetFirstString("name", out var name);
        source = source?.Trim();

        var sources = services.GetService<IOptions<AIOptions>>()?.Value.ToolInstanceSources;

        if (string.IsNullOrEmpty(source) || sources?.ContainsKey(source) != true)
        {
            return $"Nothing was saved: '{source}' is not a tool instance source. Use listAIToolInstances with includeSources to see them.";
        }

        var error = ToolInstanceSettings.TryBuildProperties(source, settings, out var properties, out _);

        if (error is not null)
        {
            return $"Nothing was saved: {error}";
        }

        var data = new JsonObject
        {
            [nameof(AIToolInstance.Source)] = source,
            [nameof(AIToolInstance.Name)] = name?.Trim(),
            [nameof(AIToolInstance.Description)] = description?.Trim(),
        };

        if (properties is not null)
        {
            data[nameof(AIToolInstance.Properties)] = properties;
        }

        var instance = await manager.NewAsync(source, data, cancellationToken);

        if (instance is null)
        {
            return "Nothing was saved: the tool instance could not be created.";
        }

        // The catalog sets the owner from the request, which a chat does not always have.
        var caller = AgentToolAuthorization.GetCaller(services);
        instance.OwnerId ??= caller?.FindFirstValue(ClaimTypes.NameIdentifier);
        instance.Author ??= caller?.Identity?.Name;

        var validation = await manager.ValidateAsync(instance, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))}";
        }

        await manager.CreateAsync(instance, cancellationToken);

        return Result(services, instance, created: true);
    }

    private static async Task<string> UpdateAsync(
        IServiceProvider services,
        ISourceCatalogManager<AIToolInstance> manager,
        string instanceId,
        string description,
        JsonObject settings,
        CancellationToken cancellationToken)
    {
        var instance = await manager.FindByIdAsync(instanceId, cancellationToken);

        if (instance is null)
        {
            return $"Nothing was saved: no tool instance has the ID '{instanceId}'. Use listAIToolInstances to find it.";
        }

        if (!await ToolInstanceSettings.CanManageAsync(services, instance))
        {
            return $"Nothing was saved: the tool instance '{instance.Name}' belongs to someone else, and you may not manage other people's tool instances.";
        }

        var editPath = AIProfileToolSupport.GetToolInstanceEditPath(services, instance.ItemId);

        if ((await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken)).IsProtectedToolInstance(instance.Name))
        {
            return AIProfileToolSupport.RunningAssistantRefusal($"the tool instance '{instance.Name}'", editPath);
        }

        var error = ToolInstanceSettings.TryBuildProperties(instance.Source, settings, out var properties, out var addressFields);

        if (error is not null)
        {
            return $"Nothing was saved: {error}";
        }

        // A stored credential goes to the address the instance names; changing the address from a chat could send it
        // elsewhere.
        if (addressFields.Count > 0 && ToolInstanceSettings.HasStoredCredential(instance))
        {
            return $"Nothing was saved: the tool instance '{instance.Name}' has a stored credential, which is sent to the address you want to change. A person must change the address on its admin page, where the credential can be entered again: {editPath}";
        }

        var data = new JsonObject();

        if (description is not null)
        {
            data[nameof(AIToolInstance.Description)] = description.Trim();
        }

        if (properties is not null)
        {
            data[nameof(AIToolInstance.Properties)] = properties;
        }

        if (data.Count == 0)
        {
            return $"Nothing was saved: no description or setting to change was given for the tool instance '{instance.Name}'.";
        }

        // Validate a merged copy first: the manager saves as it updates.
        var current = JsonSerializer.SerializeToNode(instance) as JsonObject ?? [];
        var copy = await manager.NewAsync(instance.Source, AISourceCatalogApi.Merge(current, (JsonObject)data.DeepClone()), cancellationToken);

        if (copy is not null)
        {
            copy.ItemId = instance.ItemId;
            copy.Name = instance.Name;

            var validation = await manager.ValidateAsync(copy, cancellationToken);

            if (!validation.Succeeded)
            {
                return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))}";
            }
        }

        // The JSON update merges the given settings into the stored ones, so stored credentials stay.
        await manager.UpdateAsync(instance, data, cancellationToken);

        return Result(services, instance, created: false);
    }

    private static string Result(IServiceProvider services, AIToolInstance instance, bool created)
    {
        var (settings, credentials) = ToolInstanceSettings.Read(instance);
        var editPath = AIProfileToolSupport.GetToolInstanceEditPath(services, instance.ItemId);

        return AgentToolResults.Json(new
        {
            created,
            updated = !created,
            id = instance.ItemId,
            name = instance.Name,
            functionName = instance.GetFunctionName(),
            source = instance.Source,
            description = instance.Description,
            settings,
            credentials = credentials.Count > 0 ? credentials : null,
            editPath,
            note = credentials.Count > 0
                ? $"Credentials are never taken from a chat. If the instance needs an API key, token, password or client secret, a person enters it on the admin page: {editPath}"
                : null,
        });
    }
}
