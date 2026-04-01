---
title: Custom AI Tools
sidebar_position: 4
---

# Creating Custom AI Tools

AI Tools extend the capabilities of your AI profiles by allowing the AI model to call custom functions during conversations. Tools can perform calculations, look up data, call external APIs, or trigger application workflows.

## How It Works

1. You create a tool class extending `AIFunction` (from `Microsoft.Extensions.AI`)
2. You register it in your DI container using `AddAITool<T>(name)`
3. Users attach the tool to AI profiles via the admin UI
4. During conversations, the AI model can invoke the tool when relevant

## Creating a Tool

### Step 1: Define the Tool Class

```csharp
using System.Text.Json;
using Microsoft.Extensions.AI;

public sealed class CalculatorTool : AIFunction
{
    public const string TheName = "calculator";

    // Define the JSON schema for the tool's parameters.
    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "required": ["operation", "a", "b"],
          "properties": {
            "operation": {
              "type": "string",
              "enum": ["add", "subtract", "multiply", "divide"],
              "description": "The arithmetic operation to perform."
            },
            "a": { "type": "number", "description": "The first operand." },
            "b": { "type": "number", "description": "The second operand." }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;
    public override string Description => "Performs basic arithmetic on two numbers.";
    public override JsonElement JsonSchema => _jsonSchema;

    // Optional: Set Strict to true if parameters must match exactly.
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; }
        = new Dictionary<string, object> { ["Strict"] = true };

    protected override ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var operation = arguments["operation"]?.ToString();
        var a = Convert.ToDouble(arguments["a"]);
        var b = Convert.ToDouble(arguments["b"]);

        var result = operation switch
        {
            "add" => a + b,
            "subtract" => a - b,
            "multiply" => a * b,
            "divide" when b != 0 => a / b,
            _ => double.NaN,
        };

        return ValueTask.FromResult<object>(
            JsonSerializer.Serialize(new { result }));
    }
}
```

### Step 2: Register the Tool

In your `Program.cs` or service registration:

```csharp
using CrestApps.AI.ResponseHandling;

builder.Services.AddAITool<CalculatorTool>(CalculatorTool.TheName)
    .WithTitle("Calculator")
    .WithDescription("Performs basic arithmetic on two numbers.")
    .WithCategory("Utilities")     // Groups tools in the UI
    .WithPurpose(AIToolPurposes.DataRetrieval) // Optional: purpose metadata
    .Selectable();                 // Makes visible in profile configuration
```

## Registration Options

| Method | Description |
|--------|-------------|
| `.WithTitle(string)` | Display name shown in the UI |
| `.WithDescription(string)` | Description shown in the UI |
| `.WithCategory(string)` | Groups the tool under a category header |
| `.WithPurpose(string)` | Metadata tag (e.g., `AIToolPurposes.DataRetrieval`) |
| `.Selectable()` | Makes the tool visible for user selection in profile config |

### System Tools vs Selectable Tools

By default, tools are registered as **system tools** — they are invisible in the UI and managed programmatically by the orchestrator. Call `.Selectable()` to make them visible for users to attach to profiles.

```csharp
// System tool (hidden, auto-managed by orchestrator)
services.AddAITool<InternalSearchTool>("internal_search");

// Selectable tool (visible in profile config UI)
services.AddAITool<CalculatorTool>("calculator").Selectable();
```

## Accessing Services in Tools

Tools are registered as singletons but can access scoped services via `AIFunctionArguments.Services`:

```csharp
protected override async ValueTask<object> InvokeCoreAsync(
    AIFunctionArguments arguments,
    CancellationToken cancellationToken)
{
    // Access scoped services from the current request scope.
    var dbContext = arguments.Services.GetRequiredService<MyDbContext>();
    var httpClient = arguments.Services.GetRequiredService<IHttpClientFactory>()
        .CreateClient("my-api");

    var data = await dbContext.Products.ToListAsync(cancellationToken);
    return ValueTask.FromResult<object>(JsonSerializer.Serialize(data));
}
```

## Built-In Tools

The framework provides these tools out of the box:

| Tool | Name | Purpose | Selectable |
|------|------|---------|------------|
| **Generate Image** | `generate_image` | Creates images from text descriptions | No (system) |
| **Generate Chart** | `generate_chart` | Creates Chart.js configurations | No (system) |
| **Current Date/Time** | `current_date_time` | Returns the current date and time | Yes |

## Tool Authorization

Tool execution can be controlled via the `IAIToolAccessEvaluator` interface:

```csharp
public interface IAIToolAccessEvaluator
{
    Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, string toolName);
}
```

The default implementation permits all tool calls. Override it to add custom authorization:

```csharp
public sealed class MyToolAccessEvaluator : IAIToolAccessEvaluator
{
    public Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, string toolName)
    {
        // Only admins can use the "admin_search" tool.
        if (toolName == "admin_search")
        {
            return Task.FromResult(user.IsInRole("Admin"));
        }

        return Task.FromResult(true);
    }
}

// Register to override the default:
builder.Services.AddScoped<IAIToolAccessEvaluator, MyToolAccessEvaluator>();
```

## Example: Weather Lookup Tool

```csharp
public sealed class WeatherTool : AIFunction
{
    public const string TheName = "get_weather";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "required": ["city"],
          "properties": {
            "city": { "type": "string", "description": "The city name." }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;
    public override string Description => "Gets the current weather for a city.";
    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var city = arguments["city"]?.ToString();
        var httpClient = arguments.Services
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient();

        var response = await httpClient.GetStringAsync(
            $"https://api.weather.example.com/current?city={city}",
            cancellationToken);

        return response;
    }
}

// Registration:
builder.Services.AddAITool<WeatherTool>(WeatherTool.TheName)
    .WithTitle("Weather Lookup")
    .WithDescription("Gets the current weather for a city.")
    .WithCategory("External APIs")
    .Selectable();
```
