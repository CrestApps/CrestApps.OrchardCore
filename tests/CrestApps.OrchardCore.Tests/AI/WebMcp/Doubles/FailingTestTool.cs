using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp.Doubles;

/// <summary>
/// A selectable test tool that always throws.
/// </summary>
internal sealed class FailingTestTool : AIFunction
{
    public const string TheName = "failing_tool";

    public override string Name => TheName;

    public override string Description => "Always fails.";

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Secret connection string in the message.");
    }
}
