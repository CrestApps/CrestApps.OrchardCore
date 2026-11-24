using CrestApps.OrchardCore.Learning.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// Creates the Learning index tables, for the module's migration and the tests. Index names are unique across the
/// database (SQLite index names are global).
/// </summary>
public static class LearningIndexSchema
{
    /// <summary>
    /// Creates the index tables of the learner records in the <see cref="LearningConstants.CollectionName"/> collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = LearningConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<EnrollmentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("CourseId", column => column.WithLength(26))
            .Column<string>("OfferingId", column => column.Nullable().WithLength(26))
            .Column<EnrollmentStatus>("Status")
            .Column<bool>("IsOpen")
            .Column<bool>("NeedsReview", column => column.WithDefault(false))
            .Column<string>("Number", column => column.Nullable().WithLength(32))
            .Column<int>("Cycle")
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("AccessEndsUtc", column => column.Nullable())
            .Column<DateTime>("DueUtc", column => column.Nullable())
            .Column<DateTime>("CompletedUtc", column => column.Nullable())
            .Column<DateTime>("LastActivityUtc", column => column.Nullable())
            .Column<int>("ProgressPercent"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<EnrollmentIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_EnrollmentIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_Learning_EnrollmentIndex_User", "UserId", "CourseId", "IsOpen");
            table.CreateIndex("IDX_Learning_EnrollmentIndex_Course", "CourseId", "Status");
            table.CreateIndex("IDX_Learning_EnrollmentIndex_Number", "Number");
            table.CreateIndex("IDX_Learning_EnrollmentIndex_CreatedUtc", "CreatedUtc", "Id");
            table.CreateIndex("IDX_Learning_EnrollmentIndex_CompletedUtc", "CompletedUtc", "Id");
            table.CreateIndex("IDX_Learning_EnrollmentIndex_AccessEnds", "IsOpen", "AccessEndsUtc");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<EnrollmentGrantIndex>(table => table
            .Column<string>("EnrollmentId", column => column.WithLength(26))
            .Column<string>("Source", column => column.WithLength(64))
            .Column<string>("SourceId", column => column.Nullable().WithLength(128)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<EnrollmentGrantIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_EnrollmentGrantIndex_Source", "Source", "SourceId");
            table.CreateIndex("IDX_Learning_EnrollmentGrantIndex_Enrollment", "EnrollmentId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<EnrollmentProgressIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EnrollmentId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("CourseId", column => column.WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<EnrollmentProgressIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_EnrollmentProgressIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_Learning_EnrollmentProgressIndex_Enrollment", "EnrollmentId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<CourseItemProgressIndex>(table => table
            .Column<string>("EnrollmentId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("CourseId", column => column.WithLength(26))
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<CourseItemState>("State")
            .Column<DateTime>("LastAccessUtc")
            .Column<DateTime>("CompletedUtc", column => column.Nullable())
            .Column<long>("SecondsSpent")
            .Column<decimal>("Score", column => column.Nullable())
            .Column<bool>("SelfReported"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CourseItemProgressIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_CourseItemProgressIndex_Item", "ContentItemId", "State");
            table.CreateIndex("IDX_Learning_CourseItemProgressIndex_Enrollment", "EnrollmentId");
            table.CreateIndex("IDX_Learning_CourseItemProgressIndex_LastAccessUtc", "LastAccessUtc", "Id");
        }, collection: collection);
    }

    /// <summary>
    /// Adds the review column to the enrollment index of a site created before reviews existed.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddEnrollmentReviewColumnAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<EnrollmentIndex>(table => table
            .AddColumn<bool>("NeedsReview", column => column.WithDefault(false)), collection: LearningConstants.CollectionName);
    }

    /// <summary>
    /// Creates the index table of Learning secrets (enrollment keys, quiz access codes).
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateSecretIndexAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<LearningSecretIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Kind", column => column.WithLength(50))
            .Column<string>("OwnerId", column => column.WithLength(26)),
            collection: LearningConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<LearningSecretIndex>(table =>
            table.CreateIndex("IDX_Learning_LearningSecretIndex_Owner", "Kind", "OwnerId"), collection: LearningConstants.CollectionName);
    }

    /// <summary>
    /// Creates the index table of course staff, in the default collection where content items are.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateCourseStaffIndexAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<CourseStaffIndex>(table => table
            .Column<string>("CourseId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("Role", column => column.Nullable().WithLength(64))
            .Column<bool>("Published")
            .Column<bool>("Latest"));

        await schemaBuilder.AlterIndexTableAsync<CourseStaffIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_CourseStaffIndex_User", "UserId", "Published", "Latest");
            table.CreateIndex("IDX_Learning_CourseStaffIndex_Course", "CourseId", "Published", "Latest");
        });
    }
}
