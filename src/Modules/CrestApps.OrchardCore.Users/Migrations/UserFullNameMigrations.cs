using CrestApps.OrchardCore.Users.Core.Indexes;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Users.Migrations;

internal sealed class UserFullNameMigrations : DataMigration
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserFullNameMigrations"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public UserFullNameMigrations(ILogger<UserFullNameMigrations> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create UserFullNameIndex table. It may already exist from a previous migration.");
        }

        return 1;
    }
}
