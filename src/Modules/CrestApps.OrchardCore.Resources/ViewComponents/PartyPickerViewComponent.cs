using System.Text.Json;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Resources.ViewComponents;

/// <summary>
/// Picks people and organizations of one or several kinds (users, contacts, customers, accounts, staff, any kind a
/// module registers a party source for). Other modules invoke it by name (<c>PartyPicker</c>) from their views; it
/// searches through the <c>Admin/api/crestapps/parties/search</c> endpoint. The values are party references
/// (<c>Kind:Id</c>).
/// </summary>
public sealed class PartyPickerViewComponent : ViewComponent
{
    private readonly IPartyDirectory _directory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyPickerViewComponent"/> class.
    /// </summary>
    /// <param name="directory">The party directory.</param>
    public PartyPickerViewComponent(IPartyDirectory directory)
    {
        _directory = directory;
    }

    /// <summary>
    /// Renders the picker.
    /// </summary>
    /// <param name="name">The input name.</param>
    /// <param name="selectedValues">The party references chosen now, as <c>Kind:Id</c>.</param>
    /// <param name="kinds">The kinds to offer, or none for every kind the user may search.</param>
    /// <param name="multiple">Whether several parties can be chosen.</param>
    /// <param name="buttonText">The button text.</param>
    /// <param name="smallButton">Whether the button is small.</param>
    /// <param name="required">Whether a choice is required.</param>
    /// <returns>The view.</returns>
    public async Task<IViewComponentResult> InvokeAsync(
        string name,
        IEnumerable<string> selectedValues = null,
        IEnumerable<string> kinds = null,
        bool multiple = false,
        string buttonText = null,
        bool smallButton = false,
        bool required = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var parties = (selectedValues ?? [])
            .Select(value => PartyRef.TryParse(value, out var party) ? party : default)
            .Where(party => !party.IsEmpty)
            .Distinct()
            .ToArray();

        var names = await _directory.GetNamesAsync(parties, HttpContext.RequestAborted);

        var initial = parties
            .Select(party => new { value = party.ToString(), text = names.GetValueOrDefault(party, party.Id) })
            .ToArray();

        return View(new PartyPickerViewModel
        {
            Id = $"party-picker-{name.Replace('.', '-').Replace('[', '-').Replace(']', '-')}",
            InputName = name,
            Kinds = string.Join(',', (kinds ?? []).Where(kind => !string.IsNullOrWhiteSpace(kind))),
            Multiple = multiple,
            ButtonText = buttonText,
            SmallButton = smallButton,
            Required = required,
            InitialItemsJson = JsonSerializer.Serialize(initial),
        });
    }
}
