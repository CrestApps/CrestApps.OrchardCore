---
sidebar_label: Custom Tools
sidebar_position: 8
title: Custom AI Tools
description: Register AI-callable functions using the fluent tool builder pattern.
---

# Custom AI Tools

> Register functions that the AI model can invoke during orchestration using the fluent builder pattern.

## Quick Start

```csharp
builder.Services
    .AddCrestAppsAI()
    .AddOrchestrationServices()
    .AddAITool<WeatherTool>("get-weather")
        .WithTitle("Get Weather")
        .WithDescription("Returns current weather for a location.")
        .WithCategory("Utilities")
        .Selectable();
```

## Problem & Solution

AI models can call functions (tools) to access external data or perform actions. The framework needs a way to:

- **Register** tools with metadata (name, description, category)
- **Classify** tools as system (auto-included) or selectable (user-assignable)
- **Scope** tools dynamically based on context (profile, session, available data)
- **Control access** to tools based on permissions or context

The tool builder pattern provides a fluent API for all of this.

## Tool Types

| Type | Registration | Visibility | Use Case |
|------|-------------|-----------|----------|
| **Selectable** | `.Selectable()` | Visible in UI for profile assignment | User-facing tools (calculator, search) |
| **System** | Default (no `.Selectable()`) | Hidden, auto-included by orchestrator | Internal tools (RAG search, image gen) |

## Fluent Builder API

### `AddAITool<TTool>(name)`

Returns an `AIToolBuilder<TTool>` for fluent configuration:

| Method | Description |
|--------|-------------|
| `.WithTitle(string)` | Display title in UI |
| `.WithDescription(string)` | Description shown to the AI model |
| `.WithCategory(string)` | UI grouping category |
| `.WithPurpose(string)` | Semantic purpose tag (see below) |
| `.Selectable()` | Makes the tool visible in UI and assignable to profiles |

### Tool Purposes

Purpose tags allow the orchestrator to include tools automatically based on context:

| Constant | Value | When Auto-Included |
|----------|-------|-------------------|
| `AIToolPurposes.ContentGeneration` | `"ContentGeneration"` | When the model wants to generate images or charts |
| `AIToolPurposes.DocumentProcessing` | `"DocumentProcessing"` | When documents are attached to the session |
| `AIToolPurposes.DataSourceSearch` | `"DataSourceSearch"` | When data sources are configured |

## Implementing a Tool

Tools inherit from `AITool` (which extends `AIFunction` from `Microsoft.Extensions.AI`):

```csharp
public sealed class WeatherTool : AITool
{
    public const string TheName = "get-weather";

    // Tool parameters are defined as a record or class
    private sealed record WeatherInput(string Location, string Units = "celsius");

    protected override async Task<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var input = arguments.Deserialize<WeatherInput>();
        // Call weather API...
        return new { Temperature = 22, Condition = "Sunny", Location = input.Location };
    }
}
```

Register it:

```csharp
builder.Services
    .AddAITool<WeatherTool>(WeatherTool.TheName)
        .WithTitle("Weather")
        .WithDescription("Gets current weather for a location.")
        .WithCategory("Utilities")
        .Selectable();
```

## Tool Access Control

### `IAIToolAccessEvaluator`

Override this to control which tools are available in a given context:

```csharp
public interface IAIToolAccessEvaluator
{
    ValueTask<bool> IsAccessibleAsync(
        AIToolMetadataEntry tool,
        AICompletionContext context,
        CancellationToken cancellationToken = default);
}
```

## Custom Tool Registry Provider

Supply tools from an external source (database, API, etc.):

```csharp
public sealed class MyToolRegistryProvider : IToolRegistryProvider
{
    public async ValueTask<IEnumerable<AIToolMetadataEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        // Return tools dynamically based on context
    }
}

// Register
builder.Services.AddScoped<IToolRegistryProvider, MyToolRegistryProvider>();
```

## Complex Tool Example

A tool that queries an external API with nested object parameters, async operations, and error handling:

```csharp
public sealed class OrderLookupTool : AITool
{
    public const string TheName = "lookup-order";

    private sealed record OrderQuery(
        string OrderId,
        CustomerFilter Customer = null,
        bool IncludeLineItems = true);

    private sealed record CustomerFilter(
        string Email = null,
        string Phone = null);

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var logger = arguments.Services.GetRequiredService<ILogger<OrderLookupTool>>();
        var httpClientFactory = arguments.Services.GetRequiredService<IHttpClientFactory>();

        // Deserialize nested parameters
        var query = arguments.Deserialize<OrderQuery>();

        if (string.IsNullOrEmpty(query?.OrderId) && query?.Customer is null)
        {
            logger.LogWarning("AI tool '{ToolName}' requires an order ID or customer filter.", Name);
            return "Please provide either an order ID or customer information (email or phone).";
        }

        try
        {
            var client = httpClientFactory.CreateClient("OrderApi");

            HttpResponseMessage response;

            if (!string.IsNullOrEmpty(query.OrderId))
            {
                response = await client.GetAsync(
                    $"/api/orders/{Uri.EscapeDataString(query.OrderId)}?includeItems={query.IncludeLineItems}",
                    cancellationToken);
            }
            else
            {
                var searchParams = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(query.Customer?.Email))
                {
                    searchParams["email"] = query.Customer.Email;
                }

                if (!string.IsNullOrEmpty(query.Customer?.Phone))
                {
                    searchParams["phone"] = query.Customer.Phone;
                }

                var queryString = string.Join("&",
                    searchParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

                response = await client.GetAsync(
                    $"/api/orders/search?{queryString}",
                    cancellationToken);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Order API returned {StatusCode} for tool '{ToolName}'.",
                    response.StatusCode, Name);

                return response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "No order found matching the provided criteria."
                    : "Unable to look up the order at this time. Please try again later.";
            }

            var order = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);

            return order;
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Order API timed out for tool '{ToolName}'.", Name);
            return "The order lookup timed out. Please try again.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in tool '{ToolName}'.", Name);
            return "An error occurred while looking up the order.";
        }
    }
}
```

Register it:

```csharp
builder.Services
    .AddAITool<OrderLookupTool>(OrderLookupTool.TheName)
        .WithTitle("Order Lookup")
        .WithDescription("Looks up an order by ID or customer email/phone. Returns order details and line items.")
        .WithCategory("Commerce")
        .Selectable();
```

## Tool Error Handling

When `InvokeCoreAsync` throws an exception, the orchestrator catches it and returns an error message to the AI model so the conversation can continue. However, **best practice is to never throw from a tool**. Instead, catch exceptions and return a descriptive error string:

| Pattern | Behavior | Recommended |
|---------|----------|-------------|
| Return error string | Model sees the message and can respond to the user | ✅ Yes |
| Throw exception | Orchestrator catches, logs, returns generic error to model | ❌ Avoid |
| Return `null` | Model sees an empty result | ❌ Avoid |

```csharp
// ✅ Good: Return a user-friendly error message
protected override async ValueTask<object> InvokeCoreAsync(
    AIFunctionArguments arguments, CancellationToken cancellationToken)
{
    try
    {
        // ... tool logic
        return new { result = "success", data = someData };
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "External API call failed in tool '{ToolName}'.", Name);
        return "The external service is temporarily unavailable. Please try again later.";
    }
}

// ❌ Avoid: Letting exceptions propagate
protected override async ValueTask<object> InvokeCoreAsync(
    AIFunctionArguments arguments, CancellationToken cancellationToken)
{
    // This will crash and return a generic error to the model
    var result = await httpClient.GetStringAsync("/api/data", cancellationToken);
    return result;
}
```

:::tip
Use guard clauses with `ILogger` at each validation point. This creates a clear audit trail and gives the AI model actionable error messages that it can relay to the user.
:::

## Tool Return Types

`InvokeCoreAsync` returns `ValueTask<object>`. The framework serializes the return value to JSON before passing it to the AI model. Here's how different return types are handled:

| Return Type | Serialization | Example |
|------------|---------------|---------|
| `string` | Passed as-is | `return "The weather is sunny."` |
| Anonymous object | Serialized to JSON | `return new { temp = 22, condition = "Sunny" }` |
| Record/class | Serialized to JSON | `return new WeatherResult { Temp = 22 }` |
| `JsonElement` | Passed as raw JSON | `return JsonDocument.Parse(apiResponse).RootElement` |
| Primitive (int, bool) | Converted to string | `return 42` → `"42"` |
| `null` | Empty result | Avoid — return an error string instead |

For complex return types, use explicit JSON serialization for maximum control:

```csharp
protected override async ValueTask<object> InvokeCoreAsync(
    AIFunctionArguments arguments, CancellationToken cancellationToken)
{
    var result = await FetchDataAsync(cancellationToken);

    // Explicit serialization with custom options
    return JsonSerializer.Serialize(new
    {
        result.Id,
        result.Name,
        result.Description,
        result.CreatedUtc,
        ItemCount = result.Items.Count,
    });
}
```

:::info
Keep return values concise. Large JSON payloads consume tokens and may hit model context limits. Return only the fields the AI model needs to formulate a response.
:::

## Testing Tools

Unit test tools by creating a mock `AIFunctionArguments` with the required services:

```csharp
public sealed class WeatherToolTests
{
    [Fact]
    public async Task InvokeAsync_WithValidLocation_ShouldReturnWeatherData()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { temp = 22, condition = "Sunny" }),
            });

        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("https://api.weather.test/"),
        };

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("WeatherApi").Returns(httpClient);

        var services = new ServiceCollection()
            .AddSingleton(httpClientFactory)
            .AddLogging()
            .BuildServiceProvider();

        var tool = new WeatherTool();

        var arguments = new AIFunctionArguments(
            new Dictionary<string, object>
            {
                ["Location"] = "Seattle",
                ["Units"] = "celsius",
            })
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments);

        // Assert
        Assert.NotNull(result);
        var json = result.ToString();
        Assert.Contains("22", json);
        Assert.Contains("Sunny", json);
    }

    [Fact]
    public async Task InvokeAsync_WithMissingLocation_ShouldReturnErrorMessage()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var tool = new WeatherTool();

        var arguments = new AIFunctionArguments(
            new Dictionary<string, object>())
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("location", result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MockHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
```

:::tip
Test tools in isolation from the AI model. Focus on:
1. **Valid input** → correct API call and return value
2. **Missing/invalid input** → descriptive error string (no exceptions)
3. **External service failure** → graceful error message
4. **Cancellation** → respects `CancellationToken`
:::

## Orchard Core Integration

The [AI Tools module](../ai/tools.md) adds admin UI for managing tool assignments per profile, viewing tool metadata, and configuring tool access permissions.
