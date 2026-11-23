using CrestApps.OrchardCore.Taxation.Core.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Taxation.Migrations;

/// <summary>
/// Creates the table of tax returns.
/// </summary>
internal sealed class TaxReturnsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await TaxReturnIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}

/// <summary>
/// Creates the table of e-file submissions.
/// </summary>
internal sealed class EFilingMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await EFileSubmissionIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
