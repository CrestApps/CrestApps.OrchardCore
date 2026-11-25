using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.ViewModels;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Agreements.Drivers;

/// <summary>
/// The row of an agreement in the list, in both admin list layouts, and the general fields of its editor. The rules are
/// the agreement's handler's, for the editor and recipes alike.
/// </summary>
internal sealed class AgreementDisplayDriver : DisplayDriver<Agreement>
{
    private readonly ITimeZoneSelectListProvider _timeZones;
    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public AgreementDisplayDriver(
        ITimeZoneSelectListProvider timeZones,
        IContentManager contentManager,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AgreementDisplayDriver> stringLocalizer)
    {
        _timeZones = timeZones;
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(Agreement agreement, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{agreement.Number} {agreement.Name}".ToLowerInvariant();
        }

        return Combine(
            View("Agreement_Fields_SummaryAdmin", agreement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Agreement_Status_SummaryAdmin", agreement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("Agreement_Buttons_SummaryAdmin", agreement).Location("Actions:5"),
            View("Agreement_ActionsMenu_SummaryAdmin", agreement).Location("ActionsMenu:10"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(Agreement agreement, BuildEditorContext context)
        => Initialize<AgreementViewModel>("AgreementFields_Edit", async model =>
        {
            model.Number = agreement.Number;
            model.Name = agreement.Name;
            model.Status = agreement.Status;
            model.AccountId = agreement.AccountId;
            model.StartDate = agreement.StartDate;
            model.EndDate = agreement.EndDate;
            model.TimeZoneId = agreement.TimeZoneId;
            model.ExpiringNoticeDays = agreement.ExpiringNoticeDays;
            model.Notes = agreement.Notes;
            model.TimeZones = await _timeZones.GetTimeZoneOptionsAsync(agreement.TimeZoneId, _httpContextAccessor.HttpContext?.RequestAborted ?? default);

            if (!string.IsNullOrEmpty(agreement.AccountId) && await _contentManager.GetAsync(agreement.AccountId, VersionOptions.Latest) is { } account)
            {
                model.AccountName = account.DisplayText;
            }
        }).Location(l => l.Zone("Content", "1").Card(S["General"], "10"));

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(Agreement agreement, UpdateEditorContext context)
    {
        var model = new AgreementViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // A new end date, or a pause, gives the agreement a new notice.
        if (agreement.EndDate != model.EndDate || model.Status != AgreementStatus.Active)
        {
            agreement.ExpiringNotifiedUtc = null;
        }

        agreement.Name = model.Name?.Trim();
        agreement.Status = model.Status;
        agreement.AccountId = string.IsNullOrWhiteSpace(model.AccountId) ? null : model.AccountId.Trim();
        agreement.StartDate = model.StartDate;
        agreement.EndDate = model.EndDate;
        agreement.TimeZoneId = string.IsNullOrWhiteSpace(model.TimeZoneId) ? null : model.TimeZoneId;
        agreement.ExpiringNoticeDays = model.ExpiringNoticeDays;
        agreement.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

        return Edit(agreement, context);
    }
}
