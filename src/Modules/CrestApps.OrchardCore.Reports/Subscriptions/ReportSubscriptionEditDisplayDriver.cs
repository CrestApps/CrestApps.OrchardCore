using CrestApps.OrchardCore.Reports.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Reports.Subscriptions;

/// <summary>
/// Builds the editor of a report subscription in cards: the report, its schedule and how it is delivered.
/// </summary>
internal sealed class ReportSubscriptionEditDisplayDriver : DisplayDriver<ReportSubscriptionEditViewModel>
{
    internal readonly IStringLocalizer S;

    public ReportSubscriptionEditDisplayDriver(IStringLocalizer<ReportSubscriptionEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ReportSubscriptionEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<ReportSubscriptionEditViewModel>("ReportSubscriptionGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<ReportSubscriptionEditViewModel>("ReportSubscriptionSchedule_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Schedule"], "20")),
            Initialize<ReportSubscriptionEditViewModel>("ReportSubscriptionDelivery_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Delivery"], "30")));
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportSubscriptionEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Name,
            m => m.ReportName,
            m => m.Frequency,
            m => m.DayOfWeek,
            m => m.DayOfMonth,
            m => m.Hour,
            m => m.Recipients,
            m => m.AttachmentFormat,
            m => m.IsEnabled);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(ReportSubscriptionEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ReportSubscriptionEditViewModel source, ReportSubscriptionEditViewModel model)
    {
        model.Name = source.Name;
        model.ReportName = source.ReportName;
        model.Frequency = source.Frequency;
        model.DayOfWeek = source.DayOfWeek;
        model.DayOfMonth = source.DayOfMonth;
        model.Hour = source.Hour;
        model.Recipients = source.Recipients;
        model.AttachmentFormat = source.AttachmentFormat;
        model.IsEnabled = source.IsEnabled;
        model.Subscription = source.Subscription;
        model.Reports = source.Reports;
        model.Formats = source.Formats;
        model.CanEmail = source.CanEmail;
    }
}