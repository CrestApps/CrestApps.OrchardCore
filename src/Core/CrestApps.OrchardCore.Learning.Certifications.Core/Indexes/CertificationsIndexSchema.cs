using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Indexes;

/// <summary>
/// Creates the tables of certification paths.
/// </summary>
public static class CertificationsIndexSchema
{
    /// <summary>
    /// Creates the index tables and the renewal queue.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = CertificationsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<CertificationPathIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CertificationId", column => column.WithLength(26))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CertificationPathIndex>(table =>
        {
            table.CreateIndex("IDX_LearningCertificationPath_ItemId", "ItemId");
            table.CreateIndex("IDX_LearningCertificationPath_Certification", "CertificationId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<CertificationPathTargetIndex>(table => table
            .Column<string>("PathId", column => column.WithLength(26))
            .Column<string>("TargetId", column => column.WithLength(26))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CertificationPathTargetIndex>(table =>
            table.CreateIndex("IDX_LearningCertificationPathTarget_Target", "TargetId", "Enabled"),
            collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<CertificationPathProgressIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("PathId", column => column.WithLength(26))
            .Column<string>("CertificationId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<CertificationPathState>("State")
            .Column<int>("Cycle")
            .Column<string>("CurrentRecordId", column => column.Nullable().WithLength(26))
            .Column<int>("ExpiresOn", column => column.Nullable())
            .Column<int>("RenewalOpensOn", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<CertificationPathProgressIndex>(table =>
        {
            table.CreateIndex("IDX_LearningCertificationProgress_ItemId", "ItemId");
            table.CreateIndex("IDX_LearningCertificationProgress_PathUser", "PathId", "UserId");
            table.CreateIndex("IDX_LearningCertificationProgress_User", "UserId");
            table.CreateIndex("IDX_LearningCertificationProgress_Record", "CurrentRecordId");
            table.CreateIndex("IDX_LearningCertificationProgress_Renewal", "State", "RenewalOpensOn");
            table.CreateIndex("IDX_LearningCertificationProgress_PathState", "PathId", "State");
        }, collection: collection);

        await LeasedWorkItemIndexSchema.CreateAsync(schemaBuilder, CertificationsConstants.RenewalWorkCollectionName);
    }
}
