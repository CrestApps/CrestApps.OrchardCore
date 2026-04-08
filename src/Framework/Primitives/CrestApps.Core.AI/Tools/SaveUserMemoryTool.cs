using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Tools;

public sealed partial class SaveUserMemoryTool : AIFunction
{
    public const string TheName = "save_user_memory";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "A short, stable name that identifies the memory."
        },
        "description": {
          "type": "string",
          "description": "A short semantic description of the memory category or label, such as 'The user's preferred display name.' or 'The user's Orchard Core interest.'. This describes what the memory means, not the full stored fact."
        },
        "content": {
          "type": "string",
          "description": "The durable memory content to store, such as a preference, project, recurring topic, interest, or other reusable background detail."
        }
      },
      "required": [
        "name",
        "description",
        "content"
      ],
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => "Creates or updates a durable memory for the current authenticated user, such as a preference, project, recurring topic, interest, or other reusable background detail.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } =
        new Dictionary<string, object>()
        {
            ["Strict"] = false,
        };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var logger = arguments.Services.GetRequiredService<ILogger<SaveUserMemoryTool>>();

        if (!arguments.TryGetFirstString("name", out var name) ||
            !arguments.TryGetFirstString("description", out var description) ||
            !arguments.TryGetFirstString("content", out var content))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required arguments.", Name);
            return "'name', 'description', and 'content' arguments are required.";
        }

        var userId = AIMemoryToolHelpers.GetCurrentUserId(arguments.Services);

        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("AI tool '{ToolName}' requires an authenticated user.", Name);
            return "User memory is only available for authenticated users.";
        }

        name = name.Trim();
        description = description.Trim();
        content = content.Trim();

        if (name.Length > 256)
        {
            return "Memory names must be 256 characters or fewer.";
        }

        if (content.Length > 4000)
        {
            return "Memory content must be 4000 characters or fewer.";
        }

        if (description.Length > 1000)
        {
            return "Memory description must be 1000 characters or fewer.";
        }

        var safetyService = arguments.Services.GetRequiredService<IAIMemorySafetyService>();

        if (!safetyService.TryValidate(name, description, content, out var errorMessage))
        {
            return errorMessage;
        }

        var store = arguments.Services.GetRequiredService<IAIMemoryStore>();
        var manager = arguments.Services.GetRequiredService<ICatalogManager<AIMemoryEntry>>();
        var clock = arguments.Services.GetRequiredService<TimeProvider>();
        var utcNow = clock.GetUtcNow().UtcDateTime;
        var existingMemory = await store.FindByUserAndNameAsync(userId, name);
        var created = existingMemory is null;

        if (existingMemory is null)
        {
            existingMemory = new AIMemoryEntry
            {
                UserId = userId,
                Name = name,
                Description = description,
                CreatedUtc = utcNow,
                Content = content,
                UpdatedUtc = utcNow,
            };

            await manager.CreateAsync(existingMemory);
        }
        else
        {
            existingMemory.Name = name;
            existingMemory.Description = description;
            existingMemory.Content = content;
            existingMemory.UpdatedUtc = utcNow;
            await manager.UpdateAsync(existingMemory);
        }

        return JsonSerializer.Serialize(new
        {
            existingMemory.ItemId,
            existingMemory.Name,
            existingMemory.Description,
            existingMemory.Content,
            existingMemory.CreatedUtc,
            existingMemory.UpdatedUtc,
            Created = created,
        });
    }
}
