using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Taxation;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Taxation",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = TaxationConstants.Feature.Tables,
    Name = "Tax Tables",
    Description = "Tax jurisdictions, tax tables and the calculation methods, shared by Sales Tax and Income Tax.",
    Category = "Finance"
)]

[assembly: Feature(
    Id = TaxationConstants.Feature.Taxation,
    Name = "Sales Tax",
    Description = "Sales tax on what is sold: a provider-agnostic, extensible engine with tax categories, rules, exemptions, sourcing and the TaxationPart, used by Commerce.",
    Category = "Commerce",
    Dependencies =
    [
        TaxationConstants.Feature.Tables,
        CommerceConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Id = TaxationConstants.Feature.TaxJar,
    Name = "TaxJar",
    Description = "Works out sales tax with TaxJar for orders to the countries you choose, from each address's combined rate, in place of your own tax rules.",
    Category = "Commerce",
    Dependencies =
    [
        TaxationConstants.Feature.Taxation,
    ]
)]

[assembly: Feature(
    Id = TaxationConstants.Feature.IncomeTax,
    Name = "Income Tax",
    Description = "Taxes on income for any country: income tax withholding at every level (federal, state or province, local) and the statutory contributions on earnings (such as US Social Security, Medicare and unemployment, or the Canada Pension Plan, Employment Insurance and their Quebec counterparts), worked out by dated income tax rules over the tax tables. Payroll uses it; any module that pays income can.",
    Category = "Finance",
    Dependencies =
    [
        TaxationConstants.Feature.Tables,
    ]
)]

[assembly: Feature(
    Id = TaxationConstants.Feature.IncomeTaxUnitedStates,
    Name = "United States Income Tax",
    Description = "Form W-4 as the federal tax election and its withholding method (IRS percentage method for automated payroll), with the federal income tax, Social Security, Medicare, Additional Medicare, federal unemployment and backup withholding rules as dated recipes.",
    Category = "Finance",
    Dependencies =
    [
        TaxationConstants.Feature.IncomeTax,
    ]
)]

[assembly: Feature(
    Id = TaxationConstants.Feature.IncomeTaxCanada,
    Name = "Canada Income Tax",
    Description = "TD1 claims and the CRA payroll deductions formulas (T4127) for federal, provincial and territorial tax, Revenu Québec's formulas (TP-1015.F) for Quebec, the Canada and Quebec Pension Plans with their second tiers, Employment Insurance and the Quebec Parental Insurance Plan, with the yearly figures as dated recipes.",
    Category = "Finance",
    Dependencies =
    [
        TaxationConstants.Feature.IncomeTax,
    ]
)]
