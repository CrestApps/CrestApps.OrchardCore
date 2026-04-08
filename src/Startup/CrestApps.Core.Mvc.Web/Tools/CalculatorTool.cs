using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.Core.Mvc.Web.Tools;

/// <summary>
/// A sample AI tool that performs basic arithmetic calculations.
/// Demonstrates a tool with structured parameters and validation.
/// </summary>
public sealed class CalculatorTool : AIFunction
{
    public const string TheName = "calculator";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "required": [
        "operation",
        "a",
        "b"
      ],
      "properties": {
        "operation": {
          "type": "string",
          "enum": [
            "add",
            "subtract",
            "multiply",
            "divide"
          ],
          "description": "The arithmetic operation to perform."
        },
        "a": {
          "type": "number",
          "description": "The first operand."

        },

        "b": {

          "type": "number",

          "description": "The second operand."
        }
      },
      "additionalProperties": false

    }
    """);
    public override string Name => TheName;

    public override string Description => "Performs basic arithmetic: add, subtract, multiply, or divide two numbers.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {

        ["Strict"] = true,
    };

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {

        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("operation", out var opVal) || opVal is not string operation)
        {
            return ValueTask.FromResult<object>("""{"error":"Missing required parameter: operation"}""");
        }

        if (!TryGetNumber(arguments, "a", out var a) || !TryGetNumber(arguments, "b", out var b))
        {
            return ValueTask.FromResult<object>("""{"error":"Parameters 'a' and 'b' must be numbers."}""");
        }

        var (result, error) = operation.ToLowerInvariant() switch
        {
            "add" => (a + b, (string)null),
            "subtract" => (a - b, null),

            "multiply" => (a * b, null),
            "divide" when b != 0 => (a / b, null),
            "divide" => (0d, "Division by zero is not allowed."),
            _ => (0d, $"Unknown operation '{operation}'. Use: add, subtract, multiply, divide."),
        };

        if (error != null)

        {
            return ValueTask.FromResult<object>(JsonSerializer.Serialize(new { error }));
        }

        return ValueTask.FromResult<object>(JsonSerializer.Serialize(new
        {
            expression = $"{a} {GetSymbol(operation)} {b}",
            result,

        }));
    }

    private static bool TryGetNumber(AIFunctionArguments arguments, string key, out double value)
    {
        value = 0;

        if (!arguments.TryGetValue(key, out var raw))

        {
            return false;

        }

        if (raw is double d) { value = d; return true; }

        if (raw is int i) { value = i; return true; }

        if (raw is long l) { value = l; return true; }

        if (raw is float f) { value = f; return true; }

        if (raw is decimal m) { value = (double)m; return true; }

        if (raw is JsonElement je && je.TryGetDouble(out var jd)) { value = jd; return true; }

        return double.TryParse(raw?.ToString(), out value);
    }

    private static string GetSymbol(string operation) => operation.ToLowerInvariant() switch
    {
        "add" => "+",
        "subtract" => "-",
        "multiply" => "×",
        "divide" => "÷",
        _ => "?",
    };
}
