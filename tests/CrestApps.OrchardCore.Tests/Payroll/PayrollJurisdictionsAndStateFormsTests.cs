using CrestApps.Core;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Where a person lives and works for state and local taxes: the home address gives the country, the state and the
/// city (and any Taxation jurisdiction the address matches), the work site team's jurisdictions come first and the
/// employer's own work jurisdictions serve a business with no teams; and the W-2's state and local boxes (15 to 20).
/// </summary>
public sealed class PayrollJurisdictionsAndStateFormsTests
{
    [Fact]
    public void HomeJurisdictions_ByConvention_AreTheCountryTheStateAndTheCity()
    {
        Assert.Equal(["US", "US-MI", "US-MI-DETROIT"], PayrollHomeJurisdictions.FromAddress(new Address { Country = "us", Region = "mi", City = " Detroit " }));
        Assert.Equal(["US", "US-MI", "US-MI-STCLAIRSHORES"], PayrollHomeJurisdictions.FromAddress(new Address { Country = "US", Region = "MI", City = "St. Clair Shores" }));
        Assert.Equal(["US", "US-TX"], PayrollHomeJurisdictions.FromAddress(new Address { Country = "US", Region = "TX" }));
        Assert.Equal(["CA"], PayrollHomeJurisdictions.FromAddress(new Address { Country = "CA" }));
        Assert.Empty(PayrollHomeJurisdictions.FromAddress(null));
    }

    [Fact]
    public async Task HomeJurisdictions_AddTheTaxationJurisdictionsTheAddressMatches_OfItsCountry()
    {
        var address = new Address { Country = "US", Region = "MI", County = "Wayne", City = "Detroit" };
        var jurisdictions = new Mock<ITaxJurisdictionResolver>();

        jurisdictions.Setup(resolver => resolver.ResolveAsync(address, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TaxJurisdiction { Code = "us-mi-wayne", County = "Wayne" },
                new TaxJurisdiction { Code = "US-MI-DETROIT", City = "Detroit" },
                new TaxJurisdiction { Code = "DETROIT-SALES", City = "Detroit" },
            ]);

        var resolver = new PayrollHomeJurisdictionResolver(new ServiceCollection().AddSingleton(jurisdictions.Object).BuildServiceProvider());

        var home = await resolver.ResolveAsync(new EmployeePayProfile { HomeAddress = address }, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken);

        // A sales tax code that doesn't start with the country is no income tax jurisdiction, and Detroit is there once.
        Assert.Equal(["US", "US-MI", "US-MI-DETROIT", "US-MI-WAYNE"], home);
    }

    [Fact]
    public async Task WorkJurisdictions_AreTheWorkSiteTeams_ElseTheEmployers()
    {
        var employer = new Employer { ItemId = "shop", WorkJurisdictions = ["US-MI", "US-MI-DETROIT"] };
        var employers = new Mock<IEmployerManager>();

        employers.Setup(manager => manager.FindByIdAsync("shop", It.IsAny<CancellationToken>())).Returns(new ValueTask<Employer>(employer));

        var teams = new Mock<ITeamManager>();

        teams.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) =>
            {
                var team = new Team { ItemId = id };

                team.Put(new PayrollTeamSettings { Jurisdictions = id == "southfield" ? ["US-MI", "US-MI-SOUTHFIELD"] : [] });

                return new ValueTask<Team>(team);
            });

        var services = new ServiceCollection()
            .AddSingleton(new PayrollTeamsFeature())
            .AddSingleton(teams.Object)
            .AddSingleton(employers.Object)
            .BuildServiceProvider();

        // A business with one location and no teams: the employer's.
        Assert.Equal(["US-MI", "US-MI-DETROIT"], await PayrollWorkSites.JurisdictionsAsync(services, new EmployeePayProfile { EmployerId = "shop" }, TestContext.Current.CancellationToken));

        // A work site team with jurisdictions comes first; one without falls back to the employer's.
        Assert.Equal(["US-MI", "US-MI-SOUTHFIELD"], await PayrollWorkSites.JurisdictionsAsync(services, new EmployeePayProfile { EmployerId = "shop", WorkSiteTeamId = "southfield" }, TestContext.Current.CancellationToken));
        Assert.Equal(["US-MI", "US-MI-DETROIT"], await PayrollWorkSites.JurisdictionsAsync(services, new EmployeePayProfile { EmployerId = "shop", WorkSiteTeamId = "office" }, TestContext.Current.CancellationToken));

        // An employer handed in is used as it is.
        Assert.Equal(["US-OH"], await PayrollWorkSites.JurisdictionsAsync(services, new EmployeePayProfile { EmployerId = "shop" }, new Employer { WorkJurisdictions = ["US-OH"] }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void FormW2_StateAndLocalBoxes_AreTheIncomeTaxesBelowTheCountry_WithTheEmployersStateId()
    {
        var employer = new Employer
        {
            Registrations =
            [
                new EmployerTaxRegistration { Jurisdiction = "US", AccountId = "12-3456789" },
                new EmployerTaxRegistration { Jurisdiction = "US-MI", AccountId = "UI-555", Categories = [IncomeTaxCategories.Unemployment] },
                new EmployerTaxRegistration { Jurisdiction = "US-MI", AccountId = "ME-123", Categories = [IncomeTaxCategories.IncomeTax] },
                new EmployerTaxRegistration { Jurisdiction = "US-MI-DETROIT", AccountId = "DET-9" },
            ],
        };
        var statements = new List<PayStatement>
        {
            Statement(("US-FIT", "US", IncomeTaxCategories.IncomeTax, 2000m, 160m), ("US-MI-SIT", "US-MI", IncomeTaxCategories.IncomeTax, 2000m, 76.04m), ("US-MI-DETROIT-R", "US-MI-DETROIT", IncomeTaxCategories.IncomeTax, 2000m, 47.45m), ("US-MA-PFML-EE", "US-MA", IncomeTaxCategories.SocialInsurance, 2000m, 9.20m)),
            Statement(("US-MI-SIT", "US-MI", IncomeTaxCategories.IncomeTax, 1850m, 69.14m), ("US-MI-DETROIT-R", "US-MI-DETROIT", IncomeTaxCategories.IncomeTax, 1850m, 43.85m)),
        };

        var amounts = FormW2.JurisdictionAmounts(statements, employer);

        // Federal tax and a contribution aren't state or local income tax; the state's withholding account is its id.
        Assert.Equal(
            [("US-MI", "ME-123", (string)null, 3850m, 145.18m), ("US-MI-DETROIT", "DET-9", "DETROIT", 3850m, 91.30m)],
            amounts.Select(amount => (amount.Jurisdiction, amount.AccountId, amount.Name, amount.Wages, amount.Tax)));

        // One W-2 row: Michigan with Detroit beside it.
        var row = Assert.Single(FormW2Document.StateRows(amounts));

        Assert.Equal(("US-MI", "US-MI-DETROIT"), (row.State.Jurisdiction, row.Local.Jurisdiction));
    }

    [Fact]
    public void FormW2_StateRows_PairEachStateWithItsLocalities()
    {
        var rows = FormW2Document.StateRows(
        [
            new PayrollFilingJurisdictionAmount { Jurisdiction = "US-MI", Wages = 10m },
            new PayrollFilingJurisdictionAmount { Jurisdiction = "US-MI-DETROIT", Wages = 6m },
            new PayrollFilingJurisdictionAmount { Jurisdiction = "US-MI-GRANDRAPIDS", Wages = 4m },
            new PayrollFilingJurisdictionAmount { Jurisdiction = "US-OH-COLUMBUS", Wages = 3m },
            new PayrollFilingJurisdictionAmount { Jurisdiction = "US-VA", Wages = 2m },
        ]);

        Assert.Equal(
            [("US-MI", "US-MI-DETROIT"), (null, "US-MI-GRANDRAPIDS"), (null, "US-OH-COLUMBUS"), ("US-VA", null)],
            rows.Select(row => (row.State?.Jurisdiction, row.Local?.Jurisdiction)));
        Assert.Equal("US-OH", rows[2].Local.Region);
    }

    private static PayStatement Statement(params (string TaxCode, string Jurisdiction, string Category, decimal Wages, decimal Amount)[] taxes)
        => new()
        {
            Lines = [.. taxes.Select(tax => new PayStatementLine
            {
                Type = PayLineType.Tax,
                TaxCode = tax.TaxCode,
                Jurisdiction = tax.Jurisdiction,
                TaxCategory = tax.Category,
                Payer = PayLinePayer.Employee,
                TaxableWages = tax.Wages,
                Amount = tax.Amount,
            })],
        };
}
