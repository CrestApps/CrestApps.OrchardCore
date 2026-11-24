using System.Globalization;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Programs.Workflows;

/// <summary>
/// Triggers the workflow events of required learning and learning paths after the request commits.
/// </summary>
internal sealed class ProgramsWorkflowObserver : IRequiredLearningObserver, ILearningPathObserver
{
    public Task RequiredAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
        => Trigger(nameof(LearningRequiredEvent), assignment, requirement);

    public Task DueSoonAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, int daysBefore, CancellationToken cancellationToken = default)
        => Trigger(nameof(RequiredLearningDueSoonEvent), assignment, requirement, daysBefore);

    public Task OverdueAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
        => Trigger(nameof(RequiredLearningOverdueEvent), assignment, requirement);

    public Task ExemptedAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
        => Trigger(nameof(RequiredLearningExemptionGrantedEvent), assignment, requirement);

    public Task CompletedAsync(LearningPathProgress progress, CancellationToken cancellationToken = default)
    {
        var input = new Dictionary<string, object>
        {
            ["ProgressId"] = progress.ItemId,
            ["PathId"] = progress.PathId,
            ["UserId"] = progress.UserId,
            ["Source"] = progress.Source,
            ["SourceId"] = progress.SourceId,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(nameof(LearningPathCompletedEvent), input, progress.ItemId));

        return Task.CompletedTask;
    }

    private static Task Trigger(string name, RequiredLearningAssignment assignment, LearningRequirement requirement, int? daysBefore = null)
    {
        var input = new Dictionary<string, object>
        {
            ["AssignmentId"] = assignment.ItemId,
            ["RequirementId"] = assignment.RequirementId,
            ["RequirementName"] = requirement?.Name,
            ["TargetKind"] = requirement?.TargetKind,
            ["TargetId"] = requirement?.TargetId,
            ["UserId"] = assignment.UserId,
            ["State"] = assignment.State.ToString(),
            ["DueUtc"] = assignment.DueUtc?.ToString("O", CultureInfo.InvariantCulture),
        };

        if (daysBefore is not null)
        {
            input["DaysBefore"] = daysBefore.Value;
        }

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, assignment.ItemId));

        return Task.CompletedTask;
    }
}
