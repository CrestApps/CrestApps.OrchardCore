using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Taxation.Migrations;

/// <summary>
/// Creates the table of tax returns, and adds the generic return form as a tax form from Tax Returns' recipe when the
/// feature is turned on or a site that had it on is updated.
/// </summary>
internal sealed class TaxReturnsMigrations : DataMigration
{
    public const string GenericFormRecipeFileName = "tax-returns-generic-form.recipe.json";

    private readonly TaxFormRecipeSeeder _seeder;

    public TaxReturnsMigrations(TaxFormRecipeSeeder seeder)
    {
        _seeder = seeder;
    }

    public async Task<int> CreateAsync()
    {
        await TaxReturnIndexSchema.CreateAsync(SchemaBuilder);
        await SeedGenericFormAsync();

        return 2;
    }

    // The generic return was a form in code before; it is a tax form now.
    public async Task<int> UpdateFrom1Async()
    {
        await SeedGenericFormAsync();

        return 2;
    }

    private async Task SeedGenericFormAsync()
        => await _seeder.SeedAsync(typeof(TaxReturnsMigrations).Assembly.GetName().Name, GenericFormRecipeFileName);
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
