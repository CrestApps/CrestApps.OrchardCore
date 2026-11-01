using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp.Doubles;

/// <summary>
/// A test tool registered as a system tool, which is never selectable.
/// </summary>
internal sealed class SystemTestTool : AIFunction
{
    public const string TheName = "system_tool";

    public override string Name => TheName;

    public override string Description => "A system tool.";

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<object>("system");
    }
}
