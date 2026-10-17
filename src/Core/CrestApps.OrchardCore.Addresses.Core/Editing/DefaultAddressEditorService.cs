using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Addresses.Core.Editing;

public sealed class DefaultAddressEditorService : IAddressEditorService
{
    private readonly IAddressFormatProvider _formatProvider;
    private readonly IAddressValidator _validator;
    private readonly ICountryService _countryService;

    internal readonly IStringLocalizer S;

    public DefaultAddressEditorService(
        IAddressFormatProvider formatProvider,
        IAddressValidator validator,
        ICountryService countryService,
        IStringLocalizer<DefaultAddressEditorService> stringLocalizer)
    {
        _formatProvider = formatProvider;
        _validator = validator;
        _countryService = countryService;
        S = stringLocalizer;
    }

    public async Task<AddressEditorViewModel> BuildAsync(Address address, AddressEditorOptions options = null)
    {
        var model = new AddressEditorViewModel
        {
            Name = address?.Name,
            Company = address?.Company,
            AddressLine1 = address?.AddressLine1,
            AddressLine2 = address?.AddressLine2,
            DependentLocality = address?.DependentLocality,
            City = address?.City,
            Region = address?.Region,
            PostalCode = address?.PostalCode,
            SortingCode = address?.SortingCode,
            Country = address?.Country,
            Phone = address?.Phone,
        };

        await PopulateAsync(model, options);

        return model;
    }

    public async Task PopulateAsync(AddressEditorViewModel model, AddressEditorOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        options ??= AddressEditorOptions.Default;

        var countries = await GetOfferedCountriesAsync(options);

        if (string.IsNullOrWhiteSpace(model.Country))
        {
            model.Country = options.DefaultCountry ?? (countries.Count == 1 ? countries[0].Code : null);
        }

        model.Country = model.Country?.Trim().ToUpperInvariant();
        model.Options = options;
        model.Layout = await GetLayoutAsync(model.Country);
        model.Countries = countries
            .Select(country => new SelectListItem(country.Name, country.Code, string.Equals(country.Code, model.Country, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    public async Task<AddressValidationResult> ValidateAsync(AddressEditorViewModel model, ModelStateDictionary modelState, string prefix, AddressEditorOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(modelState);

        options ??= AddressEditorOptions.Default;

        var result = await _validator.ValidateAsync(model.ToAddress());
        var problems = result.Problems.ToList();

        if (options.ShowName && options.RequireName && string.IsNullOrWhiteSpace(model.Name))
        {
            problems.Add(new AddressProblem(AddressComponent.Name, AddressProblemKind.Missing));
        }

        // A country the page does not offer is as unknown as one that does not exist.
        if (!string.IsNullOrEmpty(result.Address.Country) &&
            options.Countries is not null &&
            !options.Countries.Contains(result.Address.Country, StringComparer.OrdinalIgnoreCase) &&
            !problems.Contains(new AddressProblem(AddressComponent.Country, AddressProblemKind.Unknown)))
        {
            problems.Add(new AddressProblem(AddressComponent.Country, AddressProblemKind.Unknown));
        }

        if (problems.Count == 0)
        {
            return result;
        }

        var format = await _formatProvider.GetFormatAsync(result.Address.Country);
        var layout = await GetLayoutAsync(result.Address.Country);
        var countryName = await GetCountryNameAsync(result.Address.Country);

        foreach (var problem in problems)
        {
            modelState.AddModelError(Key(prefix, FieldOf(problem.Component)), Describe(problem, result.Address, format, layout, countryName));
        }

        return new AddressValidationResult(result.Address, problems);
    }

    public async Task<AddressEditorLayout> GetLayoutAsync(string countryCode)
    {
        var format = await _formatProvider.GetFormatAsync(countryCode);

        // A country whose script is not Latin offers each region by its own name with the Latin one beside it.
        var regions = format.Regions
            .Select(region => new AddressEditorRegion(
                region.Code,
                region.LatinName is not null && !string.Equals(region.LatinName, region.Name, StringComparison.OrdinalIgnoreCase)
                    ? $"{region.Name} ({region.LatinName})"
                    : region.Name))
            .ToArray();

        return new AddressEditorLayout
        {
            Country = format.CountryCode,
            Lines = format.Layout
                .Select(line => (IReadOnlyList<string>)line.Components.Distinct().Select(component => component.ToString()).ToArray())
                .ToArray(),
            Required = format.Required.Select(component => component.ToString()).ToArray(),
            Labels = GetLabels(format),
            Regions = regions,
            PostalCodeExample = format.PostalCodeExamples.Count > 0 ? format.PostalCodeExamples[0] : null,
        };
    }

    private Dictionary<string, string> GetLabels(AddressFormat format)
        => new(StringComparer.Ordinal)
        {
            [nameof(AddressComponent.Name)] = S["Full name"],
            [nameof(AddressComponent.Company)] = S["Company"],
            [nameof(AddressComponent.StreetAddress)] = S["Street address"],
            [nameof(AddressComponent.DependentLocality)] = LabelOf(format.DependentLocalityLabel),
            [nameof(AddressComponent.City)] = LabelOf(format.CityLabel),
            [nameof(AddressComponent.Region)] = LabelOf(format.RegionLabel),
            [nameof(AddressComponent.PostalCode)] = LabelOf(format.PostalCodeLabel),
            [nameof(AddressComponent.SortingCode)] = S["Sorting code"],
            [nameof(AddressComponent.Country)] = S["Country"],
        };

    private string LabelOf(string kind)
        => kind switch
        {
            AddressLabels.Area => S["Area"],
            AddressLabels.County => S["County"],
            AddressLabels.Department => S["Department"],
            AddressLabels.District => S["District"],
            AddressLabels.DoSi => S["Do/Si"],
            AddressLabels.Emirate => S["Emirate"],
            AddressLabels.Island => S["Island"],
            AddressLabels.Oblast => S["Oblast"],
            AddressLabels.Parish => S["Parish"],
            AddressLabels.Prefecture => S["Prefecture"],
            AddressLabels.State => S["State"],
            AddressLabels.City => S["City"],
            AddressLabels.PostTown => S["Post town"],
            AddressLabels.Suburb => S["Suburb"],
            AddressLabels.Neighborhood => S["Neighborhood"],
            AddressLabels.Townland => S["Townland"],
            AddressLabels.VillageTownship => S["Village or township"],
            AddressLabels.Eircode => S["Eircode"],
            AddressLabels.Pin => S["PIN code"],
            AddressLabels.Zip => S["ZIP code"],
            AddressLabels.Postal => S["Postal code"],
            _ => S["Province"],
        };

    private string Describe(AddressProblem problem, Address address, AddressFormat format, AddressEditorLayout layout, string countryName)
    {
        var label = layout.LabelOf(problem.Component.ToString());

        return (problem.Component, problem.Kind) switch
        {
            (AddressComponent.Country, AddressProblemKind.Missing) => S["Choose a country."],
            (AddressComponent.Country, _) => S["Choose a country from the list."],
            (AddressComponent.Region, AddressProblemKind.Unknown) => S["Choose the {0} from the list.", label],
            (AddressComponent.PostalCode, AddressProblemKind.Invalid) when format.PostalCodeExamples.Count > 0
                => S["{0} {1} isn't valid in {2}. For example: {3}.", label, address.PostalCode, countryName, format.PostalCodeExamples[0]],
            (AddressComponent.PostalCode, AddressProblemKind.Invalid) => S["{0} {1} isn't valid in {2}.", label, address.PostalCode, countryName],
            (AddressComponent.PostalCode, AddressProblemKind.NotInRegion)
                => S["{0} {1} isn't in {2}.", label, address.PostalCode, format.FindRegion(address.Region)?.Name ?? address.Region],
            _ => S["{0} is required.", label],
        };
    }

    private async Task<IReadOnlyList<CountryInfo>> GetOfferedCountriesAsync(AddressEditorOptions options)
    {
        var countries = await _countryService.GetCountriesAsync();

        return options.Countries is null
            ? countries
            : countries.Where(country => options.Countries.Contains(country.Code, StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    private async Task<string> GetCountryNameAsync(string code)
        => string.IsNullOrEmpty(code)
            ? null
            : (await _countryService.GetCountriesAsync()).FirstOrDefault(country => string.Equals(country.Code, code, StringComparison.OrdinalIgnoreCase))?.Name ?? code;

    // The street address is two inputs; its problems belong to the first.
    private static string FieldOf(AddressComponent component)
        => component == AddressComponent.StreetAddress ? nameof(AddressEditorViewModel.AddressLine1) : component.ToString();

    private static string Key(string prefix, string field)
        => string.IsNullOrEmpty(prefix) ? field : prefix + "." + field;
}
