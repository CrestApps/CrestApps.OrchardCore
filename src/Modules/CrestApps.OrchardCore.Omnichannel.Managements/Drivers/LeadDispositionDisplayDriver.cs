using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Adds the lead status a disposition moves a lead to, in every subject. A subject action for the disposition that
/// names its own status takes precedence.
/// </summary>
internal sealed class LeadDispositionDisplayDriver : DisplayDriver<OmnichannelDisposition>
{
    private readonly INamedCatalog<LeadStatus> _statuses;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadDispositionDisplayDriver"/> class.
    /// </summary>
    /// <param name="statuses">The lead status catalog.</param>
    public LeadDispositionDisplayDriver(INamedCatalog<LeadStatus> statuses)
    {
        _statuses = statuses;
    }

    public override IDisplayResult Edit(OmnichannelDisposition disposition, BuildEditorContext context)
    {
        return Initialize<LeadDispositionViewModel>("LeadStatusDisposition_Edit", async model =>
        {
            model.SetLeadStatusId = disposition.TryGet<SetLeadStatusActionMetadata>(out var metadata) ? metadata.StatusId : null;
            model.LeadStatuses = (await _statuses.GetAllAsync())
                .Where(status => !status.IsConverted)
                .OrderBy(status => status.Order)
                .Select(status => new SelectListItem(status.Name, status.ItemId, status.ItemId == model.SetLeadStatusId))
                .ToList();
        }).Location("Content:5%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelDisposition disposition, UpdateEditorContext context)
    {
        var model = new LeadDispositionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var statuses = await _statuses.GetAllAsync();
        var statusId = statuses.Any(status => status.ItemId == model.SetLeadStatusId && !status.IsConverted)
            ? model.SetLeadStatusId
            : null;

        disposition.Put(new SetLeadStatusActionMetadata { StatusId = statusId });

        return Edit(disposition, context);
    }
}
