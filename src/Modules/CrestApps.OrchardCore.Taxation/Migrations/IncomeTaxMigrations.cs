using CrestApps.OrchardCore.Taxation.Core.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Taxation.Migrations;

/// <summary>
/// Creates the table of income tax rules.
/// </summary>
internal sealed class IncomeTaxMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await IncomeTaxRuleIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
