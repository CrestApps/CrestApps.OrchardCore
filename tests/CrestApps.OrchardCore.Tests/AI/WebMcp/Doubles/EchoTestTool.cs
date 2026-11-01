using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp.Doubles;

/// <summary>
/// A selectable test tool that answers the text it receives.
/// </summary>
internal sealed class EchoTestTool : AIFunction
{
    public const string TheName = "echo_tool";

    private static readonly JsonElement _schema = JsonDocument.Parse("""{"type":"object","properties":{"text":{"type":"string"}}}""").RootElement.Clone();

    public override string Name => TheName;

    public override string Description => "Answers the text it receives.";

    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        arguments.TryGetValue("text", out var text);

        return ValueTask.FromResult<object>($"echo: {text}");
    }
}
