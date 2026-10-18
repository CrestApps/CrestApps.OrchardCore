using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Addresses;

/// <summary>
/// The address editor lays itself out for the chosen country and puts what is wrong on the right field.
/// </summary>
public sealed class AddressEditorServiceTests
{
    private static DefaultAddressEditorService CreateEditor()
    {
        var formats = new DefaultAddressFormatProvider();
        var countries = new DefaultCountryService();

        return new DefaultAddressEditorService(formats, new DefaultAddressValidator(formats, countries), countries, new PassThroughLocalizer());
    }

    [Fact]
    public async Task TheUnitedStates_AsksForAStateFromItsListAndAZipCode()
    {
        var layout = await CreateEditor().GetLayoutAsync("US");

        Assert.Equal(["City", "Region", "PostalCode"], layout.Lines[^1]);
        Assert.Equal("State", layout.LabelOf("Region"));
        Assert.Equal("ZIP code", layout.LabelOf("PostalCode"));
        Assert.True(layout.IsRequired("Region"));
        Assert.Contains(new AddressEditorRegion("CA", "California"), layout.Regions);
        Assert.False(string.IsNullOrEmpty(layout.PostalCodeExample));
    }

    [Fact]
    public async Task Japan_OffersPrefecturesInBothScripts_AndNoCity()
    {
        var layout = await CreateEditor().GetLayoutAsync("JP");

        Assert.Equal("Prefecture", layout.LabelOf("Region"));
        Assert.False(layout.Uses("City"));
        Assert.Contains(new AddressEditorRegion("13", "東京都 (Tokyo)"), layout.Regions);
    }

    [Fact]
    public async Task TheUnitedKingdom_CallsTheCityThePostTown()
    {
        var layout = await CreateEditor().GetLayoutAsync("GB");

        Assert.Equal("Post town", layout.LabelOf("City"));
        Assert.Empty(layout.Regions);
    }

    [Fact]
    public async Task ANewAddress_StartsInTheDefaultCountry()
    {
        var model = await CreateEditor().BuildAsync(null, new AddressEditorOptions { DefaultCountry = "ca" });

        Assert.Equal("CA", model.Country);
        Assert.Equal("CA", model.Layout.Country);
        Assert.Contains(model.Countries, country => country.Selected && country.Value == "CA");
    }

    [Fact]
    public async Task AnAddressAlreadyEntered_IsLaidOutForItsCountry()
    {
        var model = await CreateEditor().BuildAsync(new Address { Country = "DE", City = "Berlin", PostalCode = "10117" });

        Assert.Equal(["PostalCode", "City"], model.Layout.Lines[^1]);
        Assert.Equal("Berlin", model.City);
    }

    [Fact]
    public async Task OnlyTheCountriesThePageOffers_AreListed_AndAccepted()
    {
        var editor = CreateEditor();
        var options = new AddressEditorOptions { Countries = ["US", "CA"] };

        var model = await editor.BuildAsync(null, options);

        Assert.Equal(["CA", "US"], model.Countries.Select(country => country.Value).Order());

        var modelState = new ModelStateDictionary();
        await editor.ValidateAsync(new AddressEditorViewModel { Country = "DE", AddressLine1 = "Unter den Linden 1", City = "Berlin", PostalCode = "10117" }, modelState, "Billing", options);

        Assert.True(modelState.ContainsKey("Billing.Country"));
    }

    [Fact]
    public async Task Problems_GoOnTheirFieldsUnderThePagesPrefix()
    {
        var modelState = new ModelStateDictionary();

        var result = await CreateEditor().ValidateAsync(
            new AddressEditorViewModel { Country = "US", City = "Mountain View", Region = "California", PostalCode = "10001" },
            modelState,
            "Billing",
            new AddressEditorOptions { RequireName = true });

        Assert.False(result.IsValid);
        Assert.Equal(
            ["Billing.AddressLine1", "Billing.Name", "Billing.PostalCode"],
            modelState.Where(entry => entry.Value.Errors.Count > 0).Select(entry => entry.Key).Order());
        Assert.Equal("ZIP code 10001 isn't in California.", Assert.Single(modelState["Billing.PostalCode"].Errors).ErrorMessage);
    }

    [Fact]
    public async Task AValidAddress_AddsNothing_AndComesBackTidied()
    {
        var modelState = new ModelStateDictionary();

        var result = await CreateEditor().ValidateAsync(
            new AddressEditorViewModel { Name = " Pat Lee ", Country = "us", AddressLine1 = "1600 Amphitheatre Pkwy", City = "Mountain View", Region = "California", PostalCode = "94043" },
            modelState,
            prefix: null);

        Assert.True(result.IsValid);
        Assert.True(modelState.IsValid);
        Assert.Equal("CA", result.Address.Region);
        Assert.Equal("Pat Lee", result.Address.Name);
    }

    [Fact]
    public void AnEditorLeftBlank_HasNoValue()
    {
        Assert.False(new AddressEditorViewModel { Country = "US" }.HasAnyValue());
        Assert.True(new AddressEditorViewModel { Country = "US", City = "Springfield" }.HasAnyValue());
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<DefaultAddressEditorService>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
