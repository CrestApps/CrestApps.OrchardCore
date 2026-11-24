using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Payroll.Core.Canada;
using CrestApps.OrchardCore.Payroll.Core.TaxForms;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Loads the tax forms Payroll's country recipes ship, so the tests work out filings from the same records a site
/// gets, as the Tax Forms page and the recipe step make them.
/// </summary>
internal static class PayrollTaxFormRecipes
{
    public const string UnitedStates = "payroll-united-states-tax-forms.recipe.json";

    public const string Canada = "payroll-canada-tax-forms.recipe.json";

    /// <summary>Gets the forms of a recipe, as JSON.</summary>
    public static IReadOnlyList<JsonObject> Forms(string recipe)
        => [.. PayrollTaxFormRecipeSeeder.Forms(JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Payroll", "Recipes", recipe))))];

    /// <summary>Gets a form of a recipe, as the record the recipe step makes of it.</summary>
    public static PayrollTaxForm Form(string recipe, string name)
        => Forms(recipe).Single(form => form["Name"]!.GetValue<string>() == name).Deserialize<PayrollTaxForm>(CatalogDeploymentSerializer.Options)!;

    /// <summary>
    /// Adds forms of a recipe through the tax form manager (its handler fills, trims and checks them), and fails when one
    /// is refused.
    /// </summary>
    public static Task SeedAsync(CalendarTestHarness harness, string recipe, params string[] names)
        => harness.RunAsync(async services =>
        {
            var forms = Forms(recipe).Where(form => names.Length == 0 || names.Contains(form["Name"]!.GetValue<string>())).ToList();
            var added = await PayrollTaxFormRecipeSeeder.SeedAsync(services.GetRequiredService<IPayrollTaxFormManager>(), forms, NullLogger.Instance);

            Assert.Equal(forms.Count, added.Count);
        });

    /// <summary>The functions a site with Canada Payroll Filings on has.</summary>
    public static PayrollTaxFormFunctionCatalog Functions()
        => new([new PayrollTaxFormFunctions(), new CanadaTaxFormFunctions()]);

    /// <summary>
    /// Works out a person's boxes of a form from their statements, as a filing of a year would.
    /// </summary>
    public static IReadOnlyDictionary<string, decimal> PersonFields(PayrollTaxForm form, IReadOnlyList<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes, int year, Employer employer = null)
    {
        var formulas = PayrollTaxFormFormulas.Compile(form, Functions().Functions, new PassThroughStringLocalizer<PayrollTaxForm>(), out var errors);

        Assert.True(formulas is not null, string.Join(" ", errors.Select(error => error.Message)));

        return PayrollTaxFormFiling.PersonFields(formulas, Context(form, codes, year, employer), statements);
    }

    /// <summary>
    /// Prepares a filing of a form from statements, without a site: the people, the boxes and the notes.
    /// </summary>
    public static PayrollFiling Prepare(PayrollTaxForm form, IReadOnlyList<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes, int year, int quarter = 0, Employer employer = null, IReadOnlyDictionary<string, EmployeePayProfile> profiles = null, IReadOnlyList<TaxDeposit> deposits = null)
    {
        var formulas = PayrollTaxFormFormulas.Compile(form, Functions().Functions, new PassThroughStringLocalizer<PayrollTaxForm>(), out var errors);

        Assert.True(formulas is not null, string.Join(" ", errors.Select(error => error.Message)));

        var (from, to) = quarter > 0
            ? (new DateOnly(year, ((quarter - 1) * 3) + 1, 1), new DateOnly(year, ((quarter - 1) * 3) + 1, 1).AddMonths(3).AddDays(-1))
            : (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        var filing = new PayrollFiling { Form = form.Name, Year = year, Quarter = quarter };

        PayrollTaxFormFiling.Prepare(formulas, new PayrollFilingContext
        {
            Employer = employer ?? new Employer { ItemId = "employer", Name = "Contoso" },
            Filing = filing,
            From = from,
            To = to,
            Statements = [.. statements.OrderBy(statement => statement.PayDate)],
            Codes = codes,
            Profiles = profiles ?? new Dictionary<string, EmployeePayProfile>(),
            Deposits = deposits ?? [],
            Services = new ServiceCollection().BuildServiceProvider(),
        }, []);

        return filing;
    }

    private static PayrollTaxFormFormulaContext Context(PayrollTaxForm form, IReadOnlyDictionary<string, PayCode> codes, int year, Employer employer)
        => new()
        {
            Form = form,
            Employer = employer ?? new Employer { ItemId = "employer", Name = "Contoso" },
            Filing = new PayrollFiling { Form = form.Name, Year = year },
            From = new DateOnly(year, 1, 1),
            To = new DateOnly(year, 12, 31),
            Codes = codes,
            Deposits = [],
            Rules = [],
            Services = new ServiceCollection().BuildServiceProvider(),
        };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Payroll", "Recipes", UnitedStates)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
