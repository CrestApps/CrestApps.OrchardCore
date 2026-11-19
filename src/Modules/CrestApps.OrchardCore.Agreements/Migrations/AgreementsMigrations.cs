using CrestApps.OrchardCore.Agreements.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Agreements.Migrations;

/// <summary>
/// Creates the agreement table.
/// </summary>
internal sealed class AgreementsMigrations : DataMigration
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> CreateAsync()
    {
        await AgreementsIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
