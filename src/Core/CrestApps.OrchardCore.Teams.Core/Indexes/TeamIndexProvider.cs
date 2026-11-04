using CrestApps.OrchardCore.Teams.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Teams.Core.Indexes;

/// <summary>
/// Maps teams to <see cref="TeamIndex"/> rows.
/// </summary>
public sealed class TeamIndexProvider : IndexProvider<Team>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TeamIndexProvider"/> class.
    /// </summary>
    public TeamIndexProvider()
    {
        CollectionName = TeamsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<Team> context)
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
