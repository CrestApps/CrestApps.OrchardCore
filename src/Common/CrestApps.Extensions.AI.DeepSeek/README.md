# CrestApps.Extensions.AI.DeepSeek

Provides an implementation of the `IChatClient` interface for the `DeepSeek` cloud service (https://platform.deepseek.com/).

## Install the package

From the command-line:

```console
dotnet add CrestApps.Extensions.AI.DeepSeek
```

Or directly in the C# project file:

```xml
<ItemGroup>
  <PackageReference Include="CrestApps.Extensions.AI.DeepSeek" Version="[CURRENTVERSION]" />
</ItemGroup>
```

## Usage Examples

### Chat

```csharp
using Microsoft.Extensions.AI;
using CrestApps.Extensions.AI.DeepSeek;

IChatClient client =
    new DeepSeekChatClient(_httpClientFactory, "deepseek-chat");

Console.WriteLine(await client.CompleteAsync("What is AI?"));
```

### Chat + Conversation History

```csharp
using Microsoft.Extensions.AI;
using CrestApps.Extensions.AI.DeepSeek;

IChatClient client =
    new DeepSeekChatClient(_httpClientFactory, "deepseek-chat");

Console.WriteLine(await client.CompleteAsync(
[
    new ChatMessage(ChatRole.System, "You are a helpful AI assistant"),
    new ChatMessage(ChatRole.User, "What is AI?"),
]));
```

### Chat streaming

```csharp
using Microsoft.Extensions.AI;
using CrestApps.Extensions.AI.DeepSeek;

IChatClient client =
    new DeepSeekChatClient(_httpClientFactory, "deepseek-chat");

await foreach (var update in client.CompleteStreamingAsync("What is AI?"))
{
    Console.Write(update);
}
```

### Tool calling

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;
using CrestApps.Extensions.AI.DeepSeek;

IChatClient client =
    new DeepSeekChatClient(_httpClientFactory, "deepseek-chat");

ChatOptions chatOptions = new()
{
    Tools = [AIFunctionFactory.Create(GetWeather)]
};

await foreach (var message in client.CompleteStreamingAsync("Do I need an umbrella?", chatOptions))
{
    Console.Write(message);
}

[Description("Gets the weather")]
static string GetWeather() => Random.Shared.NextDouble() > 0.5 ? "It's sunny" : "It's raining";
```
