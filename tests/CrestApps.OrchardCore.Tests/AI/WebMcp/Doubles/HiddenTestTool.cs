using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp.Doubles;

/// <summary>
/// A test tool hidden from the tool pickers, which is never selectable.
/// </summary>
internal sealed class HiddenTestTool : AIFunction
{
    public const string TheName = "hidden_tool";

    public override string Name => TheName;

    public override string Description => "A hidden tool.";

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<object>("hidden");
    }
}
