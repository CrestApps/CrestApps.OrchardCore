using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp.Doubles;

/// <summary>
/// A selectable test tool that reports whether it received the request services.
/// </summary>
internal sealed class ServicesProbeTestTool : AIFunction
{
    public const string TheName = "services_probe_tool";

    public override string Name => TheName;

    public override string Description => "Reports whether it received the request services.";

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<object>(arguments.Services is null ? "no services" : "services");
    }
}
