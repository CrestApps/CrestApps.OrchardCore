using System.Globalization;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The rows of the employer list and the employer editor: details, contact, tax registrations and the address.
/// </summary>
internal sealed class EmployerDisplayDriver : DisplayDriver<Employer>
{
    private const int BlankRegistrationRows = 2;

    private readonly IAddressEditorService _addressEditor;
    private readonly IShapeFactory _shapeFactory;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public EmployerDisplayDriver(IAddressEditorService addressEditor, IShapeFactory shapeFactory, ISiteService siteService, IStringLocalizer<EmployerDisplayDriver> stringLocalizer)
    {
        _addressEditor = addressEditor;
        _shapeFactory = shapeFactory;
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(Employer employer, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{employer.Name} {employer.LegalName}".ToLowerInvariant();
        }

        return CombineAsync(
            View("Employer_Fields_SummaryAdmin", employer)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Employer_Tags_SummaryAdmin", employer)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("Employer_Buttons_SummaryAdmin", employer).Location("Actions:5"),
            View("Employer_ActionsMenu_SummaryAdmin", employer).Location("ActionsMenu:10"));
    }

    public override async Task<IDisplayResult> EditAsync(Employer employer, BuildEditorContext context)
    {
        if (context.IsNew && string.IsNullOrEmpty(employer.Currency))
        {
            var settings = await _siteService.GetSettingsAsync<PayrollSettings>();

            employer.Currency ??= settings.DefaultCurrency;
            employer.CountryPack ??= settings.DefaultCountryPack;
        }

        return Combine(
            Details(employer),
            await AddressAsync(await _addressEditor.BuildAsync(employer.Address, AddressOptions(employer.Address?.Country))));
    }

    public override async Task<IDisplayResult> UpdateAsync(Employer employer, UpdateEditorContext context)
    {
        var model = new EmployerViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        employer.Name = model.Name;
        employer.LegalName = model.LegalName;
        employer.TaxId = model.TaxId;
        employer.CountryPack = model.CountryPack;
        employer.Currency = model.Currency;
        employer.LogoPath = string.IsNullOrWhiteSpace(model.LogoPath) ? null : model.LogoPath.Trim();
        employer.SignatoryName = model.SignatoryName;
        employer.SignatoryTitle = model.SignatoryTitle;
        employer.ContactName = model.ContactName;
        employer.ContactEmail = model.ContactEmail;
        employer.ContactPhone = model.ContactPhone;
        employer.Status = model.Active ? EmployerStatus.Active : EmployerStatus.Inactive;
        employer.Registrations = ReadRegistrations(model.Registrations, context);
        employer.WorkJurisdictions = [.. (model.WorkJurisdictions ?? string.Empty).Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        var address = new EmployerAddressViewModel();

        await context.Updater.TryUpdateModelAsync(address, Prefix + "Address");

        address.Address ??= new AddressEditorViewModel();

        if (!address.Address.HasAnyValue())
        {
            employer.Address = null;
        }
        else
        {
            var result = await _addressEditor.ValidateAsync(address.Address, context.Updater.ModelState, $"{Prefix}Address.{nameof(address.Address)}", AddressOptions(address.Address.Country));

            if (result.IsValid)
            {
                employer.Address = result.Address;
            }
        }

        await _addressEditor.PopulateAsync(address.Address, AddressOptions(address.Address.Country));

        return Combine(Details(employer), await AddressAsync(address.Address));
    }

    /// <summary>
    /// Reads the registration rows: rows of the same jurisdiction, agency and account are one registration with several
    /// dated rates. A rate is typed with a dot for decimals.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <param name="context">The editor context, for errors; or <see langword="null"/>.</param>
    /// <returns>The registrations.</returns>
    internal IList<EmployerTaxRegistration> ReadRegistrations(IEnumerable<EmployerRegistrationRow> rows, UpdateEditorContext context)
    {
        var registrations = new List<EmployerTaxRegistration>();

        foreach (var row in rows ?? [])
        {
            if (row is null || (string.IsNullOrWhiteSpace(row.Jurisdiction) && string.IsNullOrWhiteSpace(row.Agency) && string.IsNullOrWhiteSpace(row.AccountId) && string.IsNullOrWhiteSpace(row.Rate)))
            {
                continue;
            }

            var jurisdiction = row.Jurisdiction?.Trim().ToUpperInvariant();
            var registration = registrations.FirstOrDefault(item => item.Jurisdiction == jurisdiction && item.Agency == row.Agency?.Trim() && item.AccountId == row.AccountId?.Trim());

            if (registration is null)
            {
                registration = new EmployerTaxRegistration
                {
                    Jurisdiction = jurisdiction,
                    Agency = row.Agency?.Trim(),
                    AccountId = row.AccountId?.Trim(),
                    DepositFrequency = string.IsNullOrWhiteSpace(row.DepositFrequency) ? null : row.DepositFrequency.Trim(),
                    Categories = [.. (row.Categories ?? string.Empty).Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                };
                registrations.Add(registration);
            }

            if (string.IsNullOrWhiteSpace(row.Rate))
            {
                continue;
            }

            if (decimal.TryParse(row.Rate.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate))
            {
                // A rate without its first day is the handler's to refuse, as for a recipe.
                registration.Rates.Add(new DatedRate { EffectiveFrom = row.RateFrom ?? default, Rate = rate });
            }
            else
            {
                context?.Updater.ModelState.AddModelError(Prefix, nameof(EmployerViewModel.Registrations), S["A registration's rate needs a number with a dot for decimals, such as 2.7."]);
            }
        }

        return registrations;
    }

    private CombinedResult Details(Employer employer)
        => Combine(
            Initialize<EmployerViewModel>("EmployerFields_Edit", model =>
            {
                model.Name = employer.Name;
                model.LegalName = employer.LegalName;
                model.TaxId = employer.TaxId;
                model.CountryPack = employer.CountryPack;
                model.Currency = employer.Currency;
                model.LogoPath = employer.LogoPath;
                model.Active = employer.Status == EmployerStatus.Active;
            }).Location(l => l.Zone("Content", "1").Card(S["Employer"], "10")),
            Initialize<EmployerViewModel>("EmployerContact_Edit", model =>
            {
                model.SignatoryName = employer.SignatoryName;
                model.SignatoryTitle = employer.SignatoryTitle;
                model.ContactName = employer.ContactName;
                model.ContactEmail = employer.ContactEmail;
                model.ContactPhone = employer.ContactPhone;
            }).Location(l => l.Zone("Content", "1").Card(S["Signatory and Contact"], "20")),
            Initialize<EmployerViewModel>("EmployerRegistrations_Edit", model =>
            {
                model.WorkJurisdictions = string.Join(", ", employer.WorkJurisdictions ?? []);

                foreach (var registration in employer.Registrations)
                {
                    var rates = registration.Rates.Count == 0 ? [null] : registration.Rates.OrderBy(rate => rate.EffectiveFrom).Cast<DatedRate>().ToList();

                    foreach (var rate in rates)
                    {
                        model.Registrations.Add(new EmployerRegistrationRow
                        {
                            Jurisdiction = registration.Jurisdiction,
                            Agency = registration.Agency,
                            AccountId = registration.AccountId,
                            DepositFrequency = registration.DepositFrequency,
                            Categories = string.Join(", ", registration.Categories ?? []),
                            Rate = rate?.Rate.ToString("0.####", CultureInfo.InvariantCulture),
                            RateFrom = rate?.EffectiveFrom,
                        });
                    }
                }

                for (var i = 0; i < BlankRegistrationRows; i++)
                {
                    model.Registrations.Add(new EmployerRegistrationRow());
                }
            }).Location(l => l.Zone("Content", "1").Card(S["Tax Registrations"], "30")));

    private async Task<ShapeResult> AddressAsync(AddressEditorViewModel address)
    {
        var editor = await _shapeFactory.CreateAddressEditorAsync(address, $"{Prefix}Address.{nameof(EmployerAddressViewModel.Address)}");

        return Initialize<EmployerAddressViewModel>("EmployerAddress_Edit", model =>
        {
            model.Address = address;
            model.AddressEditor = editor;
        }).Location(l => l.Zone("Content", "3").Card(S["Address"], "40"));
    }

    private static AddressEditorOptions AddressOptions(string country)
        => new()
        {
            ShowName = false,
            ShowCompany = false,
            DefaultCountry = string.IsNullOrWhiteSpace(country) ? null : country,
        };
}
