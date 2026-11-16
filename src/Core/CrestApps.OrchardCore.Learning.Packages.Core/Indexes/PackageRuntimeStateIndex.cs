using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Indexes;

/// <summary>
/// Indexes runtime states by enrollment, package item, learner, course, status and dates.
/// </summary>
public sealed class PackageRuntimeStateIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the enrollment.</summary>
    public string EnrollmentId { get; set; }

    /// <summary>Gets or sets the learner.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the course.</summary>
    public string CourseId { get; set; }

    /// <summary>Gets or sets the package item.</summary>
    public string PackageItemId { get; set; }

    /// <summary>Gets or sets the status: <c>cmi.core.lesson_status</c> for SCORM 1.2, the evaluated success or completion
    /// status for SCORM 2004 (<see cref="PackageStateSummary"/>).</summary>
    public string LessonStatus { get; set; }

    /// <summary>Gets or sets when the package was last launched, in UTC.</summary>
    public DateTime? LastLaunchedUtc { get; set; }
}

/// <summary>
/// Maps runtime states.
/// </summary>
public sealed class PackageRuntimeStateIndexProvider : IndexProvider<PackageRuntimeState>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PackageRuntimeStateIndexProvider"/> class.
    /// </summary>
    public PackageRuntimeStateIndexProvider()
    {
        CollectionName = PackagesConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PackageRuntimeState> context)
        => context.For<PackageRuntimeStateIndex>()
            .Map(state => new PackageRuntimeStateIndex
            {
                ItemId = state.ItemId,
                EnrollmentId = state.EnrollmentId,
                UserId = state.UserId,
                CourseId = state.CourseId,
                PackageItemId = state.PackageItemId,
                LessonStatus = PackageStateSummary.Of(state).Status,
                LastLaunchedUtc = state.LastLaunchedUtc,
            });
}

/// <summary>
/// Creates the index table of runtime states.
/// </summary>
public static class PackagesIndexSchema
{
    /// <summary>
    /// Creates the table and its database indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = PackagesConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<PackageRuntimeStateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EnrollmentId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("CourseId", column => column.Nullable().WithLength(26))
            .Column<string>("PackageItemId", column => column.WithLength(26))
            .Column<string>("LessonStatus", column => column.Nullable().WithLength(20))
            .Column<DateTime>("LastLaunchedUtc", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<PackageRuntimeStateIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_PackageRuntimeIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_Learning_PackageRuntimeIndex_Enrollment", "EnrollmentId", "PackageItemId");
            table.CreateIndex("IDX_Learning_PackageRuntimeIndex_Package", "PackageItemId", "LastLaunchedUtc");
            table.CreateIndex("IDX_Learning_PackageRuntimeIndex_User", "UserId");
        }, collection: collection);
    }
}
