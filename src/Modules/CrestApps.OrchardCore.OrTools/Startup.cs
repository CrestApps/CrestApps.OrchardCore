using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.OrTools.Services;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OrTools;

/// <summary>
/// Adds the CP-SAT shift schedule solver and makes it the one auto-scheduling uses.
/// </summary>
[Feature(OrToolsConstants.Features.ShiftScheduling)]
public sealed class ShiftSchedulingStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IShiftScheduleSolver, OrToolsShiftScheduleSolver>();
        services.Configure<ShiftPlanningOptions>(options => options.DefaultSolver = OrToolsConstants.SolverName);
    }
}

/// <summary>
/// Adds the routing optimizer; Dispatch uses the optimizer registered last.
/// </summary>
[Feature(OrToolsConstants.Features.Dispatch)]
public sealed class DispatchStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IDispatchOptimizer, OrToolsDispatchOptimizer>();
    }
}
