using CrestApps.OrchardCore.KnowledgeBase.Reports;
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.KnowledgeBase.Navigation;

/// <summary>
/// Adds Feedback, the Knowledge Base Feedback report, to the Knowledge Base menu when the Reports feature is on.
/// </summary>
internal sealed class KnowledgeBaseFeedbackAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public KnowledgeBaseFeedbackAdminMenu(IStringLocalizer<KnowledgeBaseFeedbackAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Knowledge Base"], S["Knowledge Base"].PrefixPosition("6.5"), knowledgeBase => knowledgeBase
            .Id(KnowledgeBaseAdminMenu.MenuId)
            .Add(S["Feedback"], S["Feedback"].PrefixPosition("3"), feedback => feedback
                .Id("knowledgeBaseFeedback")
                .Action("Display", "Reports", new { area = ReportsConstants.Feature, id = KnowledgeBaseFeedbackReport.Name })
                .Permission(KnowledgeBasePermissions.ViewFeedback)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
