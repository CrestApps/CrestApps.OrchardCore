using System.Text.Json;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Tools;

public sealed class CustomChatToolSource : IAIToolSource
{
    public const string ToolSource = "CustomChat";

    public CustomChatToolSource(IStringLocalizer<CustomChatToolSource> S)
    {
        DisplayName = S["Custom Chat Tool"];
        Description = S["Invokes a Custom Chat instance."];
    }

    public string Name => ToolSource;

    public AIToolSourceType Type => AIToolSourceType.Function;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }

    public Task<AITool> CreateAsync(AIToolInstance instance)
    {
        return Task.FromResult<AITool>(new CustomChatFunction(instance));
    }

    private sealed class CustomChatFunction : AIFunction
    {
        public CustomChatFunction(AIToolInstance instance)
        {
            Name = instance.ItemId;
            Description = instance.DisplayText;

            JsonSchema = JsonSerializer.Deserialize<JsonElement>(
                """
                {
                  "type": "object",
                  "properties": {
                    "message": { "type": "string" }
                  },
                  "required": ["message"]
                }
                """);
        }

        public override string Name { get; }

        public override string Description { get; }

        public override JsonElement JsonSchema { get; }

        protected override ValueTask<object> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<object>(
                "Custom Chat tool invoked. Execution not implemented.");
        }
    }
}
