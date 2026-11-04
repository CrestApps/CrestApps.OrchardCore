using CrestApps.OrchardCore.Skills.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Skills.Core.Indexes;

/// <summary>
/// Creates the Skills index tables, for the module's migration and the tests.
/// </summary>
public static class SkillsIndexSchema
{
    /// <summary>
    /// Creates the index tables in the <see cref="SkillsConstants.CollectionName"/> collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = SkillsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<SkillIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("NormalizedName", column => column.Nullable().WithLength(255))
            .Column<string>("Category", column => column.Nullable().WithLength(255))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SkillIndex>(table =>
        {
            table.CreateIndex("IDX_SkillIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SkillIndex_Name", "NormalizedName");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<CertificationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("NormalizedName", column => column.Nullable().WithLength(255))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CertificationIndex>(table =>
        {
            table.CreateIndex("IDX_CertificationIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_CertificationIndex_Name", "NormalizedName");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<SkillAssignmentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<int>("SubjectKind")
            .Column<string>("SubjectId", column => column.WithLength(50))
            .Column<string>("SkillId", column => column.WithLength(26))
            .Column<int>("Proficiency")
            .Column<int>("EffectiveFrom", column => column.Nullable())
            .Column<int>("EffectiveTo", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SkillAssignmentIndex>(table =>
        {
            table.CreateIndex("IDX_SkillAssignmentIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SkillAssignmentIndex_Subject", "SubjectKind", "SubjectId");
            table.CreateIndex("IDX_SkillAssignmentIndex_Skill", "SkillId", "SubjectKind", "Proficiency");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<CertificationRecordIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<int>("SubjectKind")
            .Column<string>("SubjectId", column => column.WithLength(50))
            .Column<string>("SubjectName", column => column.Nullable().WithLength(255))
            .Column<string>("CertificationId", column => column.WithLength(26))
            .Column<string>("Number", column => column.Nullable().WithLength(100))
            .Column<CertificationStatus>("Status")
            .Column<int>("IssuedOn", column => column.Nullable())
            .Column<int>("ExpiresOn", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CertificationRecordIndex>(table =>
        {
            table.CreateIndex("IDX_CertificationRecordIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_CertificationRecordIndex_Subject", "SubjectKind", "SubjectId");
            table.CreateIndex("IDX_CertificationRecordIndex_Expiry", "Status", "ExpiresOn");
            table.CreateIndex("IDX_CertificationRecordIndex_Certification", "CertificationId", "Status");
        }, collection: collection);

        await AddSubjectKindNameColumnsAsync(schemaBuilder);
    }

    /// <summary>
    /// Adds the <c>SubjectKindName</c> column, the open string kind of the record that holds a skill or certification,
    /// to the assignment and certification record indexes, with the indexes the queries seek through.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddSubjectKindNameColumnsAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = SkillsConstants.CollectionName;

        await schemaBuilder.AlterIndexTableAsync<SkillAssignmentIndex>(table =>
            table.AddColumn<string>("SubjectKindName", column => column.Nullable().WithLength(50)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SkillAssignmentIndex>(table =>
        {
            table.CreateIndex("IDX_SkillAssignmentIndex_SubjectName", "SubjectKindName", "SubjectId");
            table.CreateIndex("IDX_SkillAssignmentIndex_SkillKind", "SkillId", "SubjectKindName", "Proficiency");
        }, collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CertificationRecordIndex>(table =>
            table.AddColumn<string>("SubjectKindName", column => column.Nullable().WithLength(50)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CertificationRecordIndex>(table =>
            table.CreateIndex("IDX_CertificationRecordIndex_SubjectName", "SubjectKindName", "SubjectId"),
            collection: collection);
    }

    /// <summary>
    /// Fills in <c>SubjectKindName</c> for the rows indexed while the kind was a number (0 for a user, 1 for a worker).
    /// It only updates rows whose name is missing, so running it again changes nothing.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <param name="store">The store, for the schema the tables live in.</param>
    /// <returns>A task.</returns>
    public static async Task BackfillSubjectKindNamesAsync(ISchemaBuilder schemaBuilder, IStore store)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);

        foreach (var indexType in new[] { typeof(SkillAssignmentIndex), typeof(CertificationRecordIndex) })
        {
            var tableName = schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetIndexTable(indexType, SkillsConstants.CollectionName);
            var table = schemaBuilder.Dialect.QuoteForTableName(tableName, store.Configuration.Schema);
            var name = schemaBuilder.Dialect.QuoteForColumnName("SubjectKindName");
            var number = schemaBuilder.Dialect.QuoteForColumnName("SubjectKind");

            await using var command = schemaBuilder.Connection.CreateCommand();

            command.Transaction = schemaBuilder.Transaction;
            command.CommandText =
                $"UPDATE {table} SET {name} = CASE {number} WHEN 0 THEN '{SkillSubjectKind.User}' WHEN 1 THEN '{SkillSubjectKind.Worker}' END " +
                $"WHERE {name} IS NULL AND {number} IN (0, 1)";

            await command.ExecuteNonQueryAsync();
        }
    }
}
