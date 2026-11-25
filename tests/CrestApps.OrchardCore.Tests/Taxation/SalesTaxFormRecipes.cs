using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// Loads the generic return Tax Returns' recipe ships, builds a jurisdiction's return the way the Tax Forms page builds
/// one, and works a return out with a sales tax form without a site.
/// </summary>
internal static class SalesTaxFormRecipes
{
    public const string GenericRecipe = "tax-returns-generic-form.recipe.json";

    /// <summary>Gets the forms of the generic recipe, as JSON.</summary>
    public static IReadOnlyList<JsonObject> Forms()
        => [.. TaxFormRecipeSeeder.Forms(JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Taxation", "Recipes", GenericRecipe))))];

    /// <summary>Gets the generic return form, as the record the recipe step makes of it.</summary>
    public static TaxForm GenericForm()
        => Forms().Single().Deserialize<TaxForm>(CatalogDeploymentSerializer.Options)!;

    /// <summary>
    /// Adds the generic form through the tax form manager (its handlers fill and check it), and fails when it is refused.
    /// </summary>
    public static Task SeedAsync(CalendarTestHarness harness)
        => harness.RunAsync(async services =>
        {
            var added = await TaxFormRecipeSeeder.SeedAsync(services.GetRequiredService<ITaxFormManager>(), Forms(), NullLogger.Instance);

            Assert.Single(added);
        });

    /// <summary>The functions a site with Tax Returns on has.</summary>
    public static TaxFormFunctionCatalog Functions()
        => new([new TaxFormFunctions(), new SalesTaxFormFunctions()]);

    /// <summary>
    /// A state's monthly return as the Tax Forms page builds it: filing fields only, the lines read by kind, jurisdiction,
    /// category and form line, a discount for filing on time, a penalty of 5% a month or part of a month up to 25%,
    /// interest by the day, a check of the registration number, and line 21 as the amount due.
    /// </summary>
    public static TaxForm MichiganStyleForm()
    {
        var form = new TaxForm
        {
            Name = "US-MI-TEST",
            DisplayName = "Test monthly sales, use and withholding return",
            Source = TaxationConstants.SalesTaxFormSource,
            Jurisdiction = "US-MI",
            Values =
            [
                new TaxFormValue { Name = "DiscountRate", Year = 2020, Amount = 0.005m },
                new TaxFormValue { Name = "DiscountCap", Year = 2020, Amount = 15m },
                new TaxFormValue { Name = "InterestRate", Year = 2020, Amount = 0.0365m },
            ],
            Checks =
            [
                new TaxFormCheck { Condition = "ISACCOUNT(REGISTRATION('Number'), '##-#######')", Note = "The registration number is not a Michigan account number." },
            ],
            Fields =
            [
                Field("1", "Gross sales", "AMOUNT('GrossSales')"),
                Field("2", "Sales for resale", "AMOUNT('ExemptSales', '', '', 'Resale')"),
                Field("3", "Purchases given for line 3", "LINEAMOUNT('3')"),
                Field("4", "Michigan sales tax", "TAX('TaxableSales', 'us-mi')"),
                Field("5", "Use tax", "TAX('UseTaxPurchases')"),
                Field("6", "Total tax", "[4] + [5]"),
                Field("7", "Discount", "IF(ONTIME(), LEAST(ROUND([6] * VALUE('DiscountRate'), 2), VALUE('DiscountCap')), 0)"),
                Field("8", "Penalty", "IF(ONTIME(), 0, ROUND(LEAST([6] * 0.05 * MONTHSLATE(), [6] * 0.25), 2))"),
                Field("9", "Interest", "ROUND([6] * VALUE('InterestRate') / 365 * DAYSLATE(), 2)"),
                Field("10", "Credits", "TAX('Credit')"),
                Field("21", "Amount due", "[6] - [7] + [8] + [9] - [10]"),
            ],
        };

        form.Put(new SalesTaxFormSettings { AmountDueField = "21" });

        return form;
    }

    /// <summary>
    /// Works a return out with a form, as the return service does, without a site.
    /// </summary>
    public static void Compute(TaxForm form, TaxReturn taxReturn, DateOnly? filedOn = null, MerchantTaxRegistration registration = null)
    {
        var formulas = TaxFormFormulas.Compile(form, Functions().For(form.Source), new PassThroughStringLocalizer<TaxForm>(), out var errors);

        Assert.True(formulas is not null, string.Join(" ", errors.Select(error => error.Message)));

        TaxFormReturnForm.Compute(formulas, new TaxReturnFormContext
        {
            Registration = registration ?? new MerchantTaxRegistration(),
            Return = taxReturn,
            FiledOn = filedOn,
            Services = new ServiceCollection().BuildServiceProvider(),
        });
    }

    private static TaxFormField Field(string number, string label, string formula)
        => new()
        {
            Number = number,
            Label = number + " " + label,
            Level = TaxFormFieldLevel.Filing,
            Formula = formula,
            Print = TaxFormFieldPrint.Never,
        };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Taxation", "Recipes", GenericRecipe)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
