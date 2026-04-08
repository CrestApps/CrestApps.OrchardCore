using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Tools;

public sealed class RemoveUserMemoryTool : AIFunction
{
    public const string TheName = "remove_user_memory";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The stable name of the memory to remove."
        }
      },
      "required": [
        "name"
      ],
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => "Removes a previously saved long-term memory for the current authenticated user when it should be forgotten.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } =
        new Dictionary<string, object>()
        {
            ["Strict"] = false,
        };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var logger = arguments.Services.GetRequiredService<ILogger<RemoveUserMemoryTool>>();

        if (!arguments.TryGetFirstString("name", out var name))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument 'name'.", Name);
            return "The 'name' argument is required.";
        }

        var userId = AIMemoryToolHelpers.GetCurrentUserId(arguments.Services);

        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("AI tool '{ToolName}' requires an authenticated user.", Name);
            return "User memory is only available for authenticated users.";
        }

        name = name.Trim();

        if (name.Length > 256)
        {
            return "Memory names must be 256 characters or fewer.";
        }

        var store = arguments.Services.GetRequiredService<IAIMemoryStore>();
        var manager = arguments.Services.GetRequiredService<ICatalogManager<AIMemoryEntry>>();
        var existingMemory = await store.FindByUserAndNameAsync(userId, name);

        if (existingMemory is null)
        {
            return "No saved memory was found with that name.";
        }

        await manager.DeleteAsync(existingMemory);

        return JsonSerializer.Serialize(new
        {
            existingMemory.ItemId,
            existingMemory.Name,
            Removed = true,
        });
    }
}
