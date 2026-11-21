using CrestApps.OrchardCore.Learning.Assessments.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;

/// <summary>
/// Maps grade scales.
/// </summary>
public sealed class GradeScaleIndexProvider : IndexProvider<GradeScale>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GradeScaleIndexProvider"/> class.
    /// </summary>
    public GradeScaleIndexProvider()
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<GradeScale> context)
        => context.For<GradeScaleIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(scale => new GradeScaleIndex
            {
                ItemId = scale.ItemId,
                Name = scale.Name,
            });
}
