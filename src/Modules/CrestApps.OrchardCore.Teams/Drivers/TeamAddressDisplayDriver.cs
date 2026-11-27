using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Teams.Drivers;

/// <summary>
/// The Address card of the team editor: where the team works, laid out for its country. Optional; an address that's
/// started has to be complete for its country.
/// </summary>
internal sealed class TeamAddressDisplayDriver : DisplayDriver<Team>
{
    private readonly IAddressEditorService _addressEditor;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IStringLocalizer S;

    public TeamAddressDisplayDriver(
        IAddressEditorService addressEditor,
        IShapeFactory shapeFactory,
        IStringLocalizer<TeamAddressDisplayDriver> stringLocalizer)
    {
        _addressEditor = addressEditor;
        _shapeFactory = shapeFactory;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(Team team, BuildEditorContext context)
        => await BuildAsync(await _addressEditor.BuildAsync(team.Address, EditorOptions(team.Address?.Country)));

    public override async Task<IDisplayResult> UpdateAsync(Team team, UpdateEditorContext context)
    {
        var model = new TeamAddressViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        model.Address ??= new AddressEditorViewModel();

        if (!model.Address.HasAnyValue())
        {
            team.Address = null;
        }
        else
        {
            model.Address.Name = null;
            model.Address.Company = null;

            var result = await _addressEditor.ValidateAsync(model.Address, context.Updater.ModelState, $"{Prefix}.{nameof(model.Address)}", EditorOptions(model.Address.Country));

            if (result.IsValid)
            {
                team.Address = result.Address;
            }
        }

        await _addressEditor.PopulateAsync(model.Address, EditorOptions(model.Address.Country));

        return await BuildAsync(model.Address);
    }

    private async Task<IDisplayResult> BuildAsync(AddressEditorViewModel address)
    {
        var editor = await _shapeFactory.CreateAddressEditorAsync(address, $"{Prefix}.{nameof(TeamAddressViewModel.Address)}");

        return Initialize<TeamAddressViewModel>("TeamAddress_Edit", model =>
        {
            model.Address = address;
            model.AddressEditor = editor;
        }).Location(l => l.Zone("Content", "2").Card(S["Address"], "20"));
    }

    private static AddressEditorOptions EditorOptions(string country)
        => new()
        {
            ShowName = false,
            ShowCompany = false,
            DefaultCountry = string.IsNullOrWhiteSpace(country) ? null : country,
        };
}
