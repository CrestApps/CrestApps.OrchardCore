using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.ViewModels;
using CrestApps.OrchardCore.Staff.Services;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Accounts.Drivers;

/// <summary>
/// Picks the staff who look after an account, and shows them on the account page.
/// </summary>
internal sealed class AccountTeamPartDisplayDriver : ContentPartDisplayDriver<AccountTeamPart>
{
    private readonly IStaffDirectory _directory;

    public AccountTeamPartDisplayDriver(IStaffDirectory directory)
    {
        _directory = directory;
    }

    public override IDisplayResult Display(AccountTeamPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin)
        {
            return null;
        }

        return Initialize<AccountTeamViewModel>("AccountTeamPart_Overview", async model =>
        {
            var names = await _directory.GetNamesAsync(part.TeamStaffIds.Append(part.OwnerStaffId));

            model.OwnerName = part.OwnerStaffId is { } owner ? names.GetValueOrDefault(owner) : null;
            model.OwnerStaffId = part.OwnerStaffId;
            model.Team = part.TeamStaffIds.Where(names.ContainsKey).Select(id => new KeyValuePair<string, string>(id, names[id])).ToList();
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Overview, "7"));
    }

    public override IDisplayResult Edit(AccountTeamPart part, BuildPartEditorContext context)
        => Initialize<AccountTeamEditViewModel>(GetEditorShapeType(context), model =>
        {
            model.OwnerStaffId = part.OwnerStaffId;
            model.TeamStaffIds = [.. part.TeamStaffIds];
        });

    public override async Task<IDisplayResult> UpdateAsync(AccountTeamPart part, UpdatePartEditorContext context)
    {
        var model = new AccountTeamEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.OwnerStaffId = string.IsNullOrWhiteSpace(model.OwnerStaffId) ? null : model.OwnerStaffId.Trim();
        part.TeamStaffIds = [.. (model.TeamStaffIds ?? [])
            .SelectMany(id => (id ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(id => id != part.OwnerStaffId)
            .Distinct(StringComparer.Ordinal)];

        return Edit(part, context);
    }
}
