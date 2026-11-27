using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Taxation.Migrations;

/// <summary>
/// Creates the table of tax forms.
/// </summary>
internal sealed class TaxFormMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await TaxFormIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
