using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.MailingLists.Drivers;

/// <summary>
/// The row of a mailing list in the admin list, in both layouts, and the General and Confirmation cards of its editor.
/// The rules are the list handler's, for the editor and recipes alike.
/// </summary>
internal sealed class MailingListDisplayDriver : DisplayDriver<MailingList>
{
    private readonly IMailingListMemberStore _members;

    public MailingListDisplayDriver(IMailingListMemberStore members)
    {
        _members = members;
    }

    public override IDisplayResult Display(MailingList list, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{list.DisplayText} {list.Name}".ToLowerInvariant();
        }

        return Combine(
            View("MailingList_Fields_SummaryAdmin", list)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("MailingList_Description_SummaryAdmin", list)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("MailingList_Badges_SummaryAdmin", list)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Settings:5"),
            Initialize<MailingListCountsViewModel>("MailingList_Counts_SummaryAdmin", async model =>
            {
                model.ListId = list.ItemId;
                model.Subscribed = await _members.CountAsync(list.ItemId, MailingListMemberStatus.Subscribed);
                model.Pending = await _members.CountAsync(list.ItemId, MailingListMemberStatus.Pending);
                model.NotSynced = await _members.CountNotSyncedAsync(list.ItemId);
            })
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Members:5"),
            View("MailingList_Buttons_SummaryAdmin", list).Location("Actions:5"),
            View("MailingList_ActionsMenu_SummaryAdmin", list).Location("ActionsMenu:10"));
    }

    public override IDisplayResult Edit(MailingList list, BuildEditorContext context)
        => Combine(
            Initialize<MailingListGeneralViewModel>("MailingListGeneral_Edit", model =>
            {
                model.Name = list.Name;
                model.DisplayText = list.DisplayText;
                model.Description = list.Description;
                model.ConsentText = list.ConsentText;
                model.IsEnabled = list.IsEnabled;
                model.SendWelcomeEmail = list.SendWelcomeEmail;
            }).Location("Content:1"),
            Initialize<MailingListConfirmationViewModel>("MailingListConfirmation_Edit", model =>
            {
                model.Confirmation = list.Confirmation;
                model.SkipConfirmationForVerifiedUsers = list.SkipConfirmationForVerifiedUsers;
                model.LinkLifetimeDays = Math.Max(1, (int)Math.Round(list.ConfirmationLinkLifetime.TotalDays));
            }).Location("Content:5"));

    public override async Task<IDisplayResult> UpdateAsync(MailingList list, UpdateEditorContext context)
    {
        var general = new MailingListGeneralViewModel();
        var confirmation = new MailingListConfirmationViewModel();

        await context.Updater.TryUpdateModelAsync(general, Prefix);
        await context.Updater.TryUpdateModelAsync(confirmation, Prefix);

        list.Name = general.Name?.Trim();
        list.DisplayText = general.DisplayText?.Trim();
        list.Description = string.IsNullOrWhiteSpace(general.Description) ? null : general.Description.Trim();
        list.ConsentText = string.IsNullOrWhiteSpace(general.ConsentText) ? null : general.ConsentText.Trim();
        list.IsEnabled = general.IsEnabled;
        list.SendWelcomeEmail = general.SendWelcomeEmail;

        list.Confirmation = confirmation.Confirmation;
        list.SkipConfirmationForVerifiedUsers = confirmation.SkipConfirmationForVerifiedUsers;
        list.ConfirmationLinkLifetime = TimeSpan.FromDays(Math.Clamp(confirmation.LinkLifetimeDays, 1, 30));

        return Edit(list, context);
    }
}
