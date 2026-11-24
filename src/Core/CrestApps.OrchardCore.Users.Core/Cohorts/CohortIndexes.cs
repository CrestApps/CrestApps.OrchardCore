using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Users.Core.Cohorts;

/// <summary>
/// One row per cohort, to find a cohort by id or by name and to page them by name.
/// </summary>
public sealed class CohortIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the cohort id.
    /// </summary>
    public string CohortId { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name in upper case, to compare names without case.
    /// </summary>
    public string NormalizedName { get; set; }
}

/// <summary>
/// One row per member of a cohort, to page the members and to find the cohorts of a person.
/// </summary>
public sealed class CohortMemberIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the cohort id.
    /// </summary>
    public string CohortId { get; set; }

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets when the person was added, in UTC.
    /// </summary>
    public DateTime AddedUtc { get; set; }
}

/// <summary>
/// Maps cohorts to <see cref="CohortIndex"/> rows.
/// </summary>
public sealed class CohortIndexProvider : IndexProvider<Cohort>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CohortIndexProvider"/> class.
    /// </summary>
    public CohortIndexProvider()
    {
        CollectionName = UsersConstants.CohortsCollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Cohort> context)
        => context.For<CohortIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(cohort => new CohortIndex
            {
                CohortId = cohort.CohortId,
                Name = cohort.Name,
                NormalizedName = CohortNames.Normalize(cohort.Name),
            });
}

/// <summary>
/// Maps cohort members to <see cref="CohortMemberIndex"/> rows.
/// </summary>
public sealed class CohortMemberIndexProvider : IndexProvider<CohortMember>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CohortMemberIndexProvider"/> class.
    /// </summary>
    public CohortMemberIndexProvider()
    {
        CollectionName = UsersConstants.CohortsCollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CohortMember> context)
        => context.For<CohortMemberIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(member => new CohortMemberIndex
            {
                CohortId = member.CohortId,
                UserId = member.UserId,
                AddedUtc = member.AddedUtc,
            });
}

/// <summary>
/// Creates the cohort index tables, for the module's migration and the tests.
/// </summary>
public static class CohortsIndexSchema
{
    /// <summary>
    /// Creates the index tables in the <see cref="UsersConstants.CohortsCollectionName"/> collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = UsersConstants.CohortsCollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<CohortIndex>(table => table
            .Column<string>("CohortId", column => column.WithLength(50))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("NormalizedName", column => column.Nullable().WithLength(255)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CohortIndex>(table =>
        {
            table.CreateIndex("IDX_Cohorts_CohortIndex_CohortId", "CohortId");
            table.CreateIndex("IDX_Cohorts_CohortIndex_Name", "NormalizedName");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<CohortMemberIndex>(table => table
            .Column<string>("CohortId", column => column.WithLength(50))
            .Column<string>("UserId", column => column.WithLength(255))
            .Column<DateTime>("AddedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CohortMemberIndex>(table =>
        {
            table.CreateIndex("IDX_Cohorts_CohortMemberIndex_Cohort", "CohortId", "UserId");
            table.CreateIndex("IDX_Cohorts_CohortMemberIndex_User", "UserId", "CohortId");
        }, collection: collection);
    }
}

/// <summary>
/// Compares cohort names.
/// </summary>
public static class CohortNames
{
    /// <summary>
    /// Gets the form of a name that compares without case and outer spaces.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The name trimmed and in upper case, or <see langword="null"/>.</returns>
    public static string Normalize(string name)
        => string.IsNullOrWhiteSpace(name) ? null : name.Trim().ToUpperInvariant();
}
