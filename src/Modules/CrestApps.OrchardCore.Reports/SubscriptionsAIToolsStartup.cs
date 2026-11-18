using CrestApps.Core.AI;
using CrestApps.OrchardCore.Reports.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Registers the report subscription AI tools whenever the AI feature is on. Saving, sending and deleting ask for
/// approval, because a subscription emails report data to the addresses it lists.
/// </summary>
[Feature(ReportsConstants.SubscriptionsFeature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class SubscriptionsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionsAIToolsStartup(IStringLocalizer<SubscriptionsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListReportSubscriptionsTool>(ListReportSubscriptionsTool.TheName)
            .WithTitle(S["List Report Subscriptions"])
            .WithDescription(S["Lists the scheduled report emails with their report, frequency, recipients, attachment format, enabled state, next run, last run and last error, with optional search and paging."])
            .WithCategory(S["Reports"])
            .Selectable();

        services.AddCoreAITool<SaveReportSubscriptionTool>(SaveReportSubscriptionTool.TheName)
            .WithTitle(S["Save Report Subscription"])
            .WithDescription(S["Creates a schedule that emails a built-in report daily, weekly or monthly to up to 50 addresses, or updates one by id, including enabling or disabling it. The report is emailed for the period before each run; it needs the Email feature to be configured."])
            .WithCategory(S["Reports"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<SendReportSubscriptionTool>(SendReportSubscriptionTool.TheName)
            .WithTitle(S["Send Report Subscription"])
            .WithDescription(S["Emails a subscription's report to its recipients now, for the period before now, without changing its schedule. Returns the error when the email could not be sent."])
            .WithCategory(S["Reports"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteReportSubscriptionTool>(DeleteReportSubscriptionTool.TheName)
            .WithTitle(S["Delete Report Subscription"])
            .WithDescription(S["Deletes a report subscription so its report is no longer emailed."])
            .WithCategory(S["Reports"])
            .RequireApproval()
            .Selectable();
    }
}
