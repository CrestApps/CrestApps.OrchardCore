using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.AI.Tools;

/// <summary>
/// A sample selectable AI tool that returns the current date and time.
/// Demonstrates how to create a user-selectable (non-system) tool that
/// can be attached to AI profiles.
/// </summary>
public sealed class CurrentDateTimeTool : AIFunction
{
    public const string TheName = "current_date_time";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "timezone": {
          "type": "string",
          "description": "An optional IANA timezone identifier (e.g., 'America/New_York', 'Europe/London'). Defaults to UTC if not specified."
        },
        "format": {
          "type": "string",
          "description": "An optional .NET date/time format string (e.g., 'yyyy-MM-dd', 'f', 'U'). Defaults to a full date/time representation."
        }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => "Returns the current date and time, optionally in a specific timezone and format.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var now = (arguments.Services.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();

        if (arguments.TryGetValue("timezone", out var tzValue) && tzValue is string tzString && !string.IsNullOrWhiteSpace(tzString))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tzString);
                now = TimeZoneInfo.ConvertTime(now, tz);
            }
            catch (TimeZoneNotFoundException)
            {
                // Fall back to UTC.
            }
        }

        var format = "F";

        if (arguments.TryGetValue("format", out var fmtValue) && fmtValue is string fmtString && !string.IsNullOrWhiteSpace(fmtString))
        {
            format = fmtString;
        }

        var result = now.ToString(format, CultureInfo.InvariantCulture);

        return new ValueTask<object>($"The current date and time is: {result} (UTC offset: {now.Offset})");
    }
}
