using System.Text.Json;
using Microsoft.Extensions.AI;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.System;

public sealed class ListTimeZoneTool : AIFunction
{
    public const string TheName = "listTimeZones";

    private readonly IClock _clock;

    public ListTimeZoneTool(IClock clock)
    {
        _clock = clock;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
                "additionalProperties": false,
                "required": []
            }
            """, JsonSerializerOptions);

    }

    public override string Name => TheName;

    public override string Description => "Retrieves a list of time zones from the system.";

    public override JsonElement JsonSchema { get; }

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<object>(JsonSerializer.Serialize(_clock.GetTimeZones()));
    }
}
