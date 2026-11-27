using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// Adds <b>Customers fill this in</b> to the fields of a part attached to a ticket type.
/// </summary>
[Feature(TicketsConstants.Features.Portal)]
internal sealed class TicketFieldCustomerSettingsDisplayDriver : ContentPartFieldDefinitionDisplayDriver
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    public TicketFieldCustomerSettingsDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<TicketFieldCustomerSettingsDisplayDriver> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ContentPartFieldDefinition model, BuildEditorContext context)
    {
        if (!await IsTicketFieldAsync(model))
        {
            return null;
        }

        return Initialize<TicketFieldCustomerSettingsViewModel>("TicketFieldCustomerSettings_Edit", viewModel =>
            viewModel.CustomerEditable = model.GetSettings<TicketFieldCustomerSettings>().CustomerEditable).Location(l => l.Zone("Content", "25").Card(S["Portal"], "25"));
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition model, UpdatePartFieldEditorContext context)
    {
        if (!await IsTicketFieldAsync(model))
        {
            return null;
        }

        var viewModel = new TicketFieldCustomerSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);
        context.Builder.WithSettings(new TicketFieldCustomerSettings { CustomerEditable = viewModel.CustomerEditable });

        return await EditAsync(model, context);
    }

    private async Task<bool> IsTicketFieldAsync(ContentPartFieldDefinition model)
    {
        var partName = model.PartDefinition?.Name;

        return !string.IsNullOrEmpty(partName)
            && (await _contentDefinitionManager.ListTypeDefinitionsAsync()).Any(type => type.HasPart(TicketsConstants.PartName) && type.HasPart(partName));
    }
}
