using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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

        var clock = arguments.Services.GetRequiredService<IClock>();

        return ValueTask.FromResult<object>(JsonSerializer.Serialize(clock.GetTimeZones()));
    }
}
