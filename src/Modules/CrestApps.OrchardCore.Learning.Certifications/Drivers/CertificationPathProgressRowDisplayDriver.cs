using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Services;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Certifications.Drivers;

/// <summary>
/// The row of a person on a certification path.
/// </summary>
internal sealed class CertificationPathProgressRowDisplayDriver : DisplayDriver<CertificationPathProgress>
{
    private readonly LearnerUserLookup _users;

    public CertificationPathProgressRowDisplayDriver(LearnerUserLookup users)
    {
        _users = users;
    }

    public override IDisplayResult Display(CertificationPathProgress progress, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        async ValueTask Populate(CertificationPathProgressRowViewModel model)
        {
            model.Progress = progress;
            model.UserName = (await _users.GetUserNamesAsync([progress.UserId])).TryGetValue(progress.UserId, out var name) ? name : progress.UserId;
        }

        return Combine(
            Initialize<CertificationPathProgressRowViewModel>("CertificationPathProgress_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<CertificationPathProgressRowViewModel>("CertificationPathProgress_State_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "State:1"),
            Initialize<CertificationPathProgressRowViewModel>("CertificationPathProgress_Dates_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Dates:1"),
            Initialize<CertificationPathProgressRowViewModel>("CertificationPathProgress_Buttons_SummaryAdmin", Populate).Location("Actions:5"));
    }
}
