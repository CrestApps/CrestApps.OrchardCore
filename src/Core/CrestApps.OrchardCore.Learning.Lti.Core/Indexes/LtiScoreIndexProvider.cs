using CrestApps.OrchardCore.Learning.Lti.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Indexes;

/// <summary>
/// Maps scores.
/// </summary>
public sealed class LtiScoreIndexProvider : IndexProvider<LtiScore>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LtiScoreIndexProvider"/> class.
    /// </summary>
    public LtiScoreIndexProvider()
    {
        CollectionName = LtiConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<LtiScore> context)
        => context.For<LtiScoreIndex>()
            .Map(score => new LtiScoreIndex
            {
                ItemId = score.ItemId,
                LineItemId = score.LineItemId,
                CourseId = score.CourseId,
                UserId = score.UserId,
                ToolId = score.ToolId,
            });
}
