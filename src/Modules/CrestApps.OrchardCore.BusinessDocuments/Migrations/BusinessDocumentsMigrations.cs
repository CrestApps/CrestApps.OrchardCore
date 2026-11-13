using CrestApps.OrchardCore.BusinessDocuments.Core;
using CrestApps.OrchardCore.BusinessDocuments.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.BusinessDocuments.Migrations;

internal sealed class BusinessDocumentsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<BusinessDocumentTemplateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<bool>("IsDefault"),
            collection: BusinessDocumentsStorage.CollectionName);

        // SQLite index names are global to the database, so they carry the module's name.
        await SchemaBuilder.AlterIndexTableAsync<BusinessDocumentTemplateIndex>(table => table
            .CreateIndex("IDX_BusinessDocuments_TemplateIndex_ItemId", "ItemId", "DocumentId"),
            collection: BusinessDocumentsStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<BusinessDocumentTemplateIndex>(table => table
            .CreateIndex("IDX_BusinessDocuments_TemplateIndex_Name", "Name", "DocumentId"),
            collection: BusinessDocumentsStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<BusinessDocumentTemplateIndex>(table => table
            .CreateIndex("IDX_BusinessDocuments_TemplateIndex_IsDefault", "IsDefault", "DocumentId"),
            collection: BusinessDocumentsStorage.CollectionName);

        return 1;
    }
}
