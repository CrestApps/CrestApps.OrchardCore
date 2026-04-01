using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.System;

public sealed class ListTimeZoneTool : AIFunction
{
    public const string TheName = "listTimeZones";
    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {},
      "additionalProperties": false
    }
    """);
    public override string Name => TheName;
    public override string Description => "Retrieves a list of time zones from the system.";
    public override JsonElement JsonSchema => _jsonSchema;
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<ListTimeZoneTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var clock = arguments.Services.GetRequiredService<IClock>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return ValueTask.FromResult<object>(JsonSerializer.Serialize(clock.GetTimeZones()));
    }
}
