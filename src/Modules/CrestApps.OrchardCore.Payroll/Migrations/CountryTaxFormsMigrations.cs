using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Payroll.Migrations;

/// <summary>
/// Adds the United States tax forms (Forms 941, 940, 945, W-2 and 1099-NEC) when United States Payroll Filings is turned
/// on, or when a site that had it on is updated, from the United States tax forms recipe.
/// </summary>
internal sealed class UnitedStatesTaxFormsMigrations : DataMigration
{
    public const string RecipeFileName = "payroll-united-states-tax-forms.recipe.json";

    private readonly TaxFormRecipeSeeder _seeder;

    public UnitedStatesTaxFormsMigrations(TaxFormRecipeSeeder seeder)
    {
        _seeder = seeder;
    }

    public async Task<int> CreateAsync()
    {
        await _seeder.SeedAsync(typeof(UnitedStatesTaxFormsMigrations).Assembly.GetName().Name, RecipeFileName);

        return 1;
    }
}

/// <summary>
/// Adds the Canadian tax forms (T4 and RL-1) when Canada Payroll Filings is turned on, or when a site that had it on is
/// updated, from the Canada tax forms recipe.
/// </summary>
internal sealed class CanadaTaxFormsMigrations : DataMigration
{
    public const string RecipeFileName = "payroll-canada-tax-forms.recipe.json";

    private readonly TaxFormRecipeSeeder _seeder;

    public CanadaTaxFormsMigrations(TaxFormRecipeSeeder seeder)
    {
        _seeder = seeder;
    }

    public async Task<int> CreateAsync()
    {
        await _seeder.SeedAsync(typeof(UnitedStatesTaxFormsMigrations).Assembly.GetName().Name, RecipeFileName);

        return 1;
    }
}
