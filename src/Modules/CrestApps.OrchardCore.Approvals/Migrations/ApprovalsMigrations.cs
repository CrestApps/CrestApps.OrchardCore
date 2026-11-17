using CrestApps.OrchardCore.Approvals.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Approvals.Migrations;

internal sealed class ApprovalsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ApprovalRequestIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(30))
            .Column<string>("SourceKind", column => column.WithLength(100))
            .Column<string>("SubjectId", column => column.WithLength(255))
            .Column<string>("AccessKey", column => column.Nullable().WithLength(255))
            .Column<string>("PolicyId", column => column.Nullable().WithLength(26))
            .Column<string>("RequesterUserId", column => column.Nullable().WithLength(255))
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("DueUtc", column => column.Nullable())
            .Column<DateTime>("ModifiedUtc"),
            collection: ApprovalsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ApprovalRequestIndex>(table => table
            .CreateIndex("IDX_ApprovalRequestIndex_ItemId", "ItemId", "DocumentId"),
            collection: ApprovalsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ApprovalRequestIndex>(table => table
            .CreateIndex("IDX_ApprovalRequestIndex_Subject", "SourceKind", "SubjectId", "Status", "DocumentId"),
            collection: ApprovalsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ApprovalRequestIndex>(table => table
            .CreateIndex("IDX_ApprovalRequestIndex_Access", "SourceKind", "AccessKey", "DocumentId"),
            collection: ApprovalsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ApprovalRequestIndex>(table => table
            .CreateIndex("IDX_ApprovalRequestIndex_Status", "Status", "CreatedUtc", "DocumentId"),
            collection: ApprovalsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ApprovalRequestIndex>(table => table
            .CreateIndex("IDX_ApprovalRequestIndex_Modified", "ModifiedUtc", "DocumentId"),
            collection: ApprovalsConstants.CollectionName);

        return 1;
    }
}
