using System.Globalization;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// Starts the Skills workflow events once the change that raised them is committed.
/// </summary>
internal sealed class SkillsWorkflowObserver : ICertificationExpiryObserver, ISkillAssignmentHandler
{
    public Task ExpiringAsync(CertificationRecord record, Certification certification, int daysBefore, CancellationToken cancellationToken = default)
    {
        var input = RecordInput(record, certification);

        input["DaysBefore"] = daysBefore;
        Trigger(nameof(CertificationExpiringEvent), input, record.ItemId);

        return Task.CompletedTask;
    }

    public Task ExpiredAsync(CertificationRecord record, Certification certification, CancellationToken cancellationToken = default)
    {
        Trigger(nameof(CertificationExpiredEvent), RecordInput(record, certification), record.ItemId);

        return Task.CompletedTask;
    }

    public Task AssignmentsChangedAsync(SkillAssignmentsChangedContext context, CancellationToken cancellationToken = default)
    {
        var input = new Dictionary<string, object>
        {
            ["SubjectKind"] = context.Subject.Kind.ToString(),
            ["SubjectId"] = context.Subject.Id,
            ["SkillIds"] = context.Assignments.Select(assignment => assignment.SkillId).ToArray(),
            ["ChangedSkillIds"] = context.Changed.Select(assignment => assignment.SkillId).ToArray(),
            ["RemovedSkillIds"] = context.RemovedSkillIds.ToArray(),
            ["EffectiveDatesOnly"] = context.EffectiveDatesOnly,
            ["Actor"] = context.Actor,
        };

        Trigger(nameof(SkillsChangedEvent), input, context.Subject.Key);

        return Task.CompletedTask;
    }

    private static Dictionary<string, object> RecordInput(CertificationRecord record, Certification certification)
    {
        return new Dictionary<string, object>
        {
            ["CertificationRecordId"] = record.ItemId,
            ["CertificationId"] = record.CertificationId,
            ["CertificationName"] = certification?.Name,
            ["SubjectKind"] = record.SubjectKind.ToString(),
            ["SubjectId"] = record.SubjectId,
            ["SubjectName"] = record.SubjectName,
            ["Number"] = record.Number,
            ["ExpiresOn"] = record.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }

    private static void Trigger(string name, Dictionary<string, object> input, string correlationId)
    {
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, correlationId));
    }
}
