using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps workers to <see cref="WorkerIndex"/> and <see cref="WorkerTeamMembershipIndex"/> rows.
/// </summary>
public sealed class WorkerIndexProvider : IndexProvider<WorkerProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerIndexProvider"/> class.
    /// </summary>
    public WorkerIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<WorkerProfile> context)
    {
        context.For<WorkerIndex>()
            .Map(worker => new WorkerIndex
            {
                ItemId = worker.ItemId,
                DisplayName = worker.DisplayName,
                DisplayText = worker.DisplayName,
                NormalizedEmployeeNumber = WorkforceNames.Normalize(worker.EmployeeNumber),
                UserId = string.IsNullOrEmpty(worker.UserId) ? null : worker.UserId,
                Email = worker.Email,
                Status = worker.Status,
                EmploymentType = worker.EmploymentType,
                PrimaryTeamId = worker.PrimaryTeamId,
            });

        context.For<WorkerTeamMembershipIndex>()
            .Map(worker => worker.TeamMemberships
                .Where(membership => !string.IsNullOrEmpty(membership.TeamId))
                .Select(membership => new WorkerTeamMembershipIndex
                {
                    WorkerId = worker.ItemId,
                    TeamId = membership.TeamId,
                    Role = membership.Role,
                    EffectiveFrom = WorkforceDates.ToKey(membership.EffectiveFrom),
                    EffectiveTo = WorkforceDates.ToKey(membership.EffectiveTo),
                }));
    }
}
