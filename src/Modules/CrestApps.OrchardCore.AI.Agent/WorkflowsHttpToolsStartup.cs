using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that sends a signal to waiting workflows, when the HTTP Workflows Activities feature, which
/// provides the Signal event, is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.WorkflowsHttp)]
public sealed class WorkflowsHttpToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowsHttpToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkflowsHttpToolsStartup(IStringLocalizer<WorkflowsHttpToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<TriggerWorkflowSignalTool>(TriggerWorkflowSignalTool.TheName)
            .WithTitle(S["Trigger Workflow Signal"])
            .WithDescription(S["Sends a named signal: resumes the instances waiting on a Signal event with that name for a given instance id or correlation id, and starts enabled workflows whose start activity is that signal. Use it to move a waiting workflow forward, for example an approval step."])
            .WithCategory(S["Workflow Management"])
            .AsDestructive()
            .Selectable();
    }
}
