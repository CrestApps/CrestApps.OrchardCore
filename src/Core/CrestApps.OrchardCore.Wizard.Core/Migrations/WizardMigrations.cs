using CrestApps.OrchardCore.Wizard;
using CrestApps.OrchardCore.Wizard.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Wizard.Core.Migrations;

/// <summary>
/// Creates the index table that backs wizard sessions.
/// </summary>
public sealed class WizardMigrations : DataMigration
{
    /// <summary>
    /// Creates the initial schema.
    /// </summary>
    /// <returns>The resulting schema version.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<WizardSessionIndex>(table => table
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("WizardType")
            .Column<string>("DefinitionId")
            .Column<string>("DefinitionVersionId")
            .Column<string>("OwnerId")
            .Column<WizardSessionStatus>("Status")
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("ModifiedUtc")
            .Column<DateTime>("CompletedUtc", column => column.Nullable())
        );

        await SchemaBuilder.AlterIndexTableAsync<WizardSessionIndex>(table => table
            .CreateIndex("IDX_WizardSessionIndex_SessionId", "SessionId", "Status", "OwnerId")
        );

        await SchemaBuilder.AlterIndexTableAsync<WizardSessionIndex>(table => table
            .CreateIndex("IDX_WizardSessionIndex_Definition", "WizardType", "DefinitionId")
        );

        return 1;
    }
}
