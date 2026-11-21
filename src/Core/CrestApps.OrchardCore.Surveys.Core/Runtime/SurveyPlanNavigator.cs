using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Reads a response's plan: the screens, the designed page of a screen, the questions of a screen, and the next screen
/// that shows.
/// </summary>
public static class SurveyPlanNavigator
{
    /// <summary>
    /// Gets the designed page id of a screen.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="screenId">The screen id.</param>
    /// <returns>The page id.</returns>
    public static string PageOf(SurveyResponsePlan plan, string screenId)
    {
        if (string.IsNullOrEmpty(screenId))
        {
            return null;
        }

        if (plan?.ScreenPages is not null && plan.ScreenPages.TryGetValue(screenId, out var pageId))
        {
            return pageId;
        }

        var separator = screenId.IndexOf(SurveyPlanBuilder.ScreenSeparator, StringComparison.Ordinal);

        return separator > 0 ? screenId.Substring(0, separator) : screenId;
    }

    /// <summary>
    /// Gets the question ids of a screen in plan order.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="screenId">The screen id.</param>
    /// <returns>The question ids.</returns>
    public static IReadOnlyList<string> QuestionsOf(SurveyResponsePlan plan, string screenId)
        => !string.IsNullOrEmpty(screenId) && plan?.QuestionOrder is not null && plan.QuestionOrder.TryGetValue(screenId, out var ids) ? ids : [];

    /// <summary>
    /// Gets whether a screen shows: its page shows and it has a visible question, or its page has no question at all.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="screenId">The screen.</param>
    /// <param name="state">The logic state.</param>
    /// <returns><see langword="true"/> when it shows.</returns>
    public static bool Shows(SurveyResponsePlan plan, string screenId, SurveyLogicState state)
    {
        if (state.HiddenPageIds.Contains(PageOf(plan, screenId)))
        {
            return false;
        }

        var questions = QuestionsOf(plan, screenId);

        return questions.Count == 0 || questions.Any(id => !state.HiddenQuestionIds.Contains(id));
    }

    /// <summary>
    /// Gets the first screen that shows, from a position in the plan.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="state">The logic state.</param>
    /// <param name="fromIndex">The first position to look at.</param>
    /// <returns>The screen id, or <see langword="null"/> when no later screen shows.</returns>
    public static string FirstShowing(SurveyResponsePlan plan, SurveyLogicState state, int fromIndex = 0)
    {
        for (var index = Math.Max(fromIndex, 0); index < plan.PageOrder.Count; index++)
        {
            if (Shows(plan, plan.PageOrder[index], state))
            {
                return plan.PageOrder[index];
            }
        }

        return null;
    }

    /// <summary>
    /// Gets whether a screen is the last one of its designed page.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="screenId">The screen.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool IsLastScreenOfPage(SurveyResponsePlan plan, string screenId)
    {
        var pageId = PageOf(plan, screenId);
        var index = plan.PageOrder.IndexOf(screenId);

        return index < 0 || index == plan.PageOrder.Count - 1 || !string.Equals(PageOf(plan, plan.PageOrder[index + 1]), pageId, StringComparison.Ordinal);
    }
}
