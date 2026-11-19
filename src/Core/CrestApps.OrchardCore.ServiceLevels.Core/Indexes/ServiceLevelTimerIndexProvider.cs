using CrestApps.OrchardCore.ServiceLevels.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Indexes;

/// <summary>
/// Maps service level timers to their index rows.
/// </summary>
public sealed class ServiceLevelTimerIndexProvider : IndexProvider<ServiceLevelTimer>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelTimerIndexProvider"/> class.
    /// </summary>
    public ServiceLevelTimerIndexProvider()
    {
        CollectionName = ServiceLevelsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<ServiceLevelTimer> context)
    {
        context.For<ServiceLevelTimerIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(timer => new ServiceLevelTimerIndex
            {
                ItemId = timer.ItemId,
                SubjectKind = timer.Subject.Kind,
                SubjectId = timer.Subject.Id,
                Metric = timer.Metric,
                PolicyId = timer.PolicyId,
                Priority = timer.Priority,
                State = timer.State,
                IsActive = timer.IsActive,
                Warned = timer.WarnedUtc is not null,
                Breached = timer.IsBreached,
                StartedUtc = timer.StartedUtc,
                WarnAtUtc = timer.WarnAtUtc,
                DueUtc = timer.DueUtc,
                EndedUtc = timer.EndedUtc,
                TargetSeconds = timer.TargetSeconds,
                ElapsedSeconds = timer.ElapsedSeconds,
            });
    }
}
