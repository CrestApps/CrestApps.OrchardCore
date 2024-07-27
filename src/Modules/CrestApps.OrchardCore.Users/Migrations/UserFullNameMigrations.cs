using CrestApps.OrchardCore.Users.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Users.Migrations;

public sealed class UserFullNameMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        // In version 2, we changed the package name where UserFullNameMigrations reside.
        // The UserFullNameIndex could already exists when this migration runs again.
        // Add try catch block, to let the migration run without an issue.
        try
        {
            await SchemaBuilder.CreateMapIndexTableAsync<UserFullNameIndex>(table => table
                .Column<string>("FirstName", column => column.WithLength(255))
                .Column<string>("LastName", column => column.WithLength(255))
                .Column<string>("MiddleName", column => column.WithLength(255))
                .Column<string>("DisplayName", column => column.WithLength(255))
            );

            await SchemaBuilder.AlterIndexTableAsync<UserFullNameIndex>(table => table
                .CreateIndex("IDX_UserFullNameIndex_DocumentId",
                    "DocumentId",
                    "FirstName",
                    "LastName",
                    "MiddleName"
                )
            );

            await SchemaBuilder.AlterIndexTableAsync<UserFullNameIndex>(table => table
                .CreateIndex("IDX_UserDisplayNameIndex_DocumentId",
                    "DocumentId",
                    "DisplayName"
                )
            );
        }
        catch { }

        return 1;
    }
}
