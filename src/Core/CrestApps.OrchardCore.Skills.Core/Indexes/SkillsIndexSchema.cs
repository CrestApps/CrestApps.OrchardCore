using CrestApps.OrchardCore.Skills.Models;
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
            .Column<SkillSubjectKind>("SubjectKind")
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
            .Column<SkillSubjectKind>("SubjectKind")
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
    }
}
