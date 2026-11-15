using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Recipes;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// The catalog recipe steps of Pay Setup and Payroll carry every value of a record, when it is created and when a
/// second import updates it (the handlers copy the recipe's values; they once left new records empty).
/// </summary>
public sealed class PayrollCatalogRecipeTests
{
    [Fact]
    public void PayrollFeature_DependsOnAddresses_WhoseEditorTheEmployerEditorNeeds()
    {
        // The employer editor (EmployerDisplayDriver) and processing (printed addresses) need the Addresses services; a
        // site that turned Payroll on without Addresses failed on the employers page.
        var feature = typeof(CrestApps.OrchardCore.Payroll.Drivers.EmployerDisplayDriver).Assembly
            .GetCustomAttributes(typeof(global::OrchardCore.Modules.Manifest.FeatureAttribute), inherit: false)
            .Cast<global::OrchardCore.Modules.Manifest.FeatureAttribute>()
            .Single(attribute => attribute is not global::OrchardCore.Modules.Manifest.ModuleAttribute && attribute.Id == PayrollConstants.Features.Payroll);

        Assert.Contains(CrestApps.OrchardCore.Addresses.AddressConstants.Feature.ModuleId, feature.Dependencies);
        Assert.Contains(typeof(CrestApps.OrchardCore.Payroll.Drivers.EmployerDisplayDriver).GetConstructors().SelectMany(constructor => constructor.GetParameters()), parameter => parameter.ParameterType == typeof(CrestApps.OrchardCore.Addresses.Core.Editing.IAddressEditorService));
    }

    [Fact]
    public async Task PayCodes_AreCreatedWithTheirValuesAndSettings_AndUpdatedByCode()
    {
        await using var harness = await CreateHarnessAsync();
        var step = JsonNode.Parse("""
            {
              "name": "PayCodes",
              "Codes": [
                { "Code": "ret", "Name": "Retirement plan", "Class": "Deduction", "Kind": "Contribution",
                  "Properties": { "PayrollPayCodeSettings": { "Calculation": "PercentOfEarnings", "TaxTreatment": { "IncomeTax": "Exempt" }, "Priority": 20 } } }
              ]
            }
            """)!.AsObject();

        Assert.Empty(await ImportAsync<PayCodesRecipeStep>(harness, PayrollConstants.PayCodesRecipeStepName, step));

        step["Codes"]![0]!["Name"] = "Retirement";

        Assert.Empty(await ImportAsync<PayCodesRecipeStep>(harness, PayrollConstants.PayCodesRecipeStepName, step));

        var code = Assert.Single(await harness.RunAsync(async services => (await services.GetRequiredService<IPayCodeManager>().GetAllAsync()).ToList(), commit: false));

        Assert.Equal(("RET", "Retirement", PayCodeClass.Deduction, PayCodeKinds.Contribution), (code.Code, code.Name, code.Class, code.Kind));
        Assert.Equal(PayCodeTaxTreatment.Exempt, code.TreatmentFor(PayrollTaxCategories.IncomeTax));
        Assert.Equal(20, code.GetPayrollSettings().Priority);
    }

    [Fact]
    public async Task PaySchedules_AreCreatedWithTheirValues()
    {
        await using var harness = await CreateHarnessAsync();
        var step = JsonNode.Parse("""
            { "name": "PaySchedules", "Schedules": [ { "Name": "Twice a month", "Frequency": "SemiMonthly", "SecondPeriodStartDay": 16, "PayDateOffsetDays": 3 } ] }
            """)!.AsObject();

        Assert.Empty(await ImportAsync<PaySchedulesRecipeStep>(harness, PayrollConstants.PaySchedulesRecipeStepName, step));

        var schedule = Assert.Single(await harness.RunAsync(async services => (await services.GetRequiredService<IPayScheduleManager>().GetAllAsync()).ToList(), commit: false));

        Assert.Equal(("Twice a month", PayFrequency.SemiMonthly, 3), (schedule.Name, schedule.Frequency, schedule.PayDateOffsetDays));
    }

    [Fact]
    public async Task Employers_AreCreatedWithTheirValuesAndRegistrations()
    {
        await using var harness = await CreateHarnessAsync();
        var step = JsonNode.Parse("""
            {
              "name": "PayrollEmployers",
              "Employers": [
                { "Name": "Contoso", "LegalName": "Contoso Ltd", "Currency": "usd", "CountryPack": "US",
                  "Registrations": [ { "Jurisdiction": "US", "Agency": "Revenue", "AccountId": "12-3456789" } ] }
              ]
            }
            """)!.AsObject();

        Assert.Empty(await ImportAsync<EmployersRecipeStep>(harness, PayrollConstants.EmployersRecipeStepName, step));

        var employer = Assert.Single(await harness.RunAsync(async services => (await services.GetRequiredService<IEmployerManager>().GetAllAsync()).ToList(), commit: false));

        Assert.Equal(("Contoso", "Contoso Ltd", "USD"), (employer.Name, employer.LegalName, employer.Currency));
        Assert.Equal("12-3456789", Assert.Single(employer.Registrations).AccountId);
    }

    private static Task<IReadOnlyList<string>> ImportAsync<TStep>(CalendarTestHarness harness, string name, JsonObject step)
        where TStep : global::OrchardCore.Recipes.Services.IRecipeStepHandler
        => harness.RunAsync<IReadOnlyList<string>>(async services =>
        {
            var context = new RecipeExecutionContext { Name = name, Step = step.DeepClone().AsObject() };

            await ActivatorUtilities.CreateInstance<TStep>(services).ExecuteAsync(context);

            return [.. context.Errors];
        });

    private static Task<CalendarTestHarness> CreateHarnessAsync()
        => WorkforceTests.CreateAsync(
            services =>
            {
                services.AddDataProtection();
                services.AddPayrollCore();
                services.AddPayrollHandlers();
            },
            PayrollIndexSchema.CreateAsync,
            PayrollIndexSchema.Providers());
}
