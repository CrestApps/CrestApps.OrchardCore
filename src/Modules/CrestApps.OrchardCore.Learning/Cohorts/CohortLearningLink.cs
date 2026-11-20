using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Cohorts;

/// <summary>
/// The cohorts the sync has applied to a published course or learning path: the people in them were enrolled. It is
/// compared with the item's <see cref="Models.LearningCohortsPart"/> when the item is published, so linking or
/// unlinking a cohort enrolls or takes off exactly its members, however the item was saved.
/// </summary>
public sealed class CohortLearningLink
{
    /// <summary>
    /// Gets or sets the content item id of the course or path.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the content part that marks it, which names the target that enrolls people in it.
    /// </summary>
    public string PartName { get; set; }

    /// <summary>
    /// Gets or sets the title of the item when the link was last applied, to name it where the cohort is used.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the ids of the cohorts applied.
    /// </summary>
    public List<string> CohortIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when the link was last applied, in UTC.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }
}

/// <summary>
/// One row per cohort of a <see cref="CohortLearningLink"/>, to find the courses and paths a cohort is linked to.
/// </summary>
public sealed class CohortLearningLinkIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the content item id of the course or path.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the cohort id.
    /// </summary>
    public string CohortId { get; set; }
}

/// <summary>
/// Maps links to <see cref="CohortLearningLinkIndex"/> rows.
/// </summary>
public sealed class CohortLearningLinkIndexProvider : IndexProvider<CohortLearningLink>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CohortLearningLinkIndexProvider"/> class.
    /// </summary>
    public CohortLearningLinkIndexProvider()
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CohortLearningLink> context)
        => context.For<CohortLearningLinkIndex>()
            .ExclusiveToDocumentType()
            .Map(link => (link.CohortIds ?? [])
                .Distinct(StringComparer.Ordinal)
                .Select(cohortId => new CohortLearningLinkIndex { TargetId = link.TargetId, CohortId = cohortId }));
}

/// <summary>
/// Creates the cohort link index table.
/// </summary>
internal sealed class LearningCohortsMigrations : global::OrchardCore.Data.Migration.DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CohortLearningLinkIndex>(table => table
            .Column<string>("TargetId", column => column.WithLength(26))
            .Column<string>("CohortId", column => column.WithLength(50)),
            collection: LearningConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<CohortLearningLinkIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_CohortLearningLinkIndex_Cohort", "CohortId", "TargetId");
            table.CreateIndex("IDX_Learning_CohortLearningLinkIndex_Target", "TargetId");
        }, collection: LearningConstants.CollectionName);

        return 1;
    }
}
