using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps teams to <see cref="TeamIndex"/> rows.
/// </summary>
public sealed class TeamIndexProvider : IndexProvider<WorkforceTeam>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TeamIndexProvider"/> class.
    /// </summary>
    public TeamIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<WorkforceTeam> context)
    {
        context.For<TeamIndex>()
            .Map(team => new TeamIndex
            {
                ItemId = team.ItemId,
                Name = team.Name,
                Kind = team.Kind,
                ParentId = string.IsNullOrEmpty(team.ParentId) ? null : team.ParentId,
                Enabled = team.Enabled,
            });
    }
}
