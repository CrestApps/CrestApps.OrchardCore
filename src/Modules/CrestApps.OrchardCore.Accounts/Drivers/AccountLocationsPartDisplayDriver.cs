using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.ViewModels;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Accounts.Drivers;

/// <summary>
/// The locations on an account's editor: a name and an address for each, checked against the country's rules by the
/// shared address editor. A location is addressed to the account itself, so the editor asks for neither a recipient
/// nor a company.
/// </summary>
internal sealed class AccountLocationsPartDisplayDriver : ContentPartDisplayDriver<AccountLocationsPart>
{
    private readonly IAddressEditorService _addressEditor;
    private readonly IShapeFactory _shapeFactory;

    public AccountLocationsPartDisplayDriver(IAddressEditorService addressEditor, IShapeFactory shapeFactory)
    {
        _addressEditor = addressEditor;
        _shapeFactory = shapeFactory;
    }

    public override IDisplayResult Display(AccountLocationsPart part, BuildPartDisplayContext context)
    {
        // The locations show on the account's page, in its Overview tab.
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin)
        {
            return null;
        }

        return View("AccountLocationsPart_Overview", part)
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Overview, "6"));
    }

    public override async Task<IDisplayResult> EditAsync(AccountLocationsPart part, BuildPartEditorContext context)
    {
        var rows = new List<AccountLocationRowViewModel>();
        var options = EditorOptions(part.Locations?.Select(location => location?.Address?.Country));

        foreach (var location in (part.Locations ?? []).Where(location => location is not null))
        {
            rows.Add(new AccountLocationRowViewModel
            {
                LocationId = location.LocationId,
                Name = location.Name,
                Address = await _addressEditor.BuildAsync(location.Address, options),
            });
        }

        return await BuildEditorAsync(rows, context);
    }

    public override async Task<IDisplayResult> UpdateAsync(AccountLocationsPart part, UpdatePartEditorContext context)
    {
        var model = new AccountLocationsPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var rows = model.Locations ?? [];
        var options = EditorOptions(rows.Select(row => row?.Address?.Country));
        var accountName = part.ContentItem.DisplayText?.Trim();
        var locations = new List<AccountLocation>();
        var valid = true;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] ??= new AccountLocationRowViewModel();
            row.Address ??= new AddressEditorViewModel();

            // A row left blank is no location.
            if (string.IsNullOrWhiteSpace(row.Name) && !row.Address.HasAnyValue())
            {
                continue;
            }

            row.Address.Name = accountName;
            row.Address.Company = accountName;

            var result = await _addressEditor.ValidateAsync(row.Address, context.Updater.ModelState, AddressPrefix(i), options);

            if (!result.IsValid)
            {
                valid = false;

                continue;
            }

            locations.Add(new AccountLocation
            {
                LocationId = string.IsNullOrEmpty(row.LocationId) ? IdGenerator.GenerateId() : row.LocationId,
                Name = string.IsNullOrWhiteSpace(row.Name) ? result.Address.City : row.Name.Trim(),
                Address = result.Address,
            });
        }

        if (!valid)
        {
            // The rows are shown again as posted, at the same indexes, so each address's errors stay with it.
            return await BuildEditorAsync(rows, context, keepBlankRows: true);
        }

        part.Locations = locations;

        return await EditAsync(part, context);
    }

    private async Task<IDisplayResult> BuildEditorAsync(List<AccountLocationRowViewModel> rows, BuildPartEditorContext context, bool keepBlankRows = false)
    {
        if (!keepBlankRows || rows.Count == 0)
        {
            rows.Add(new AccountLocationRowViewModel());
        }

        var options = EditorOptions(rows.Select(row => row.Address?.Country));

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            row.Address ??= new AddressEditorViewModel();

            await _addressEditor.PopulateAsync(row.Address, options);
            row.AddressEditor = await _shapeFactory.CreateAddressEditorAsync(row.Address, AddressPrefix(i));
        }

        return Initialize<AccountLocationsPartViewModel>(GetEditorShapeType(context), model => model.Locations = rows)
            .Location(AccountPageTabs.EditorLocation(AccountPageTabs.Locations));
    }

    private string AddressPrefix(int index)
        => $"{Prefix}.{nameof(AccountLocationsPartViewModel.Locations)}[{index}].{nameof(AccountLocationRowViewModel.Address)}";

    // A new location starts in the country of the account's first location.
    private static AddressEditorOptions EditorOptions(IEnumerable<string> countries)
        => new()
        {
            ShowName = false,
            ShowCompany = false,
            DefaultCountry = countries?.FirstOrDefault(country => !string.IsNullOrWhiteSpace(country)),
        };
}
