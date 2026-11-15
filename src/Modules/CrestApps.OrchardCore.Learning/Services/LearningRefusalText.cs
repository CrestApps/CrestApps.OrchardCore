using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Turns the refusal codes of the Learning services into localized text for people.
/// </summary>
public sealed class LearningRefusalText
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningRefusalText"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningRefusalText(IStringLocalizer<LearningRefusalText> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the text of a refusal code. An unknown code, such as one of a custom release rule, is returned as is.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The text, or <see langword="null"/> without a code.</returns>
    public LocalizedString Get(string code)
        => code switch
        {
            null or "" => null,
            LearningRefusals.ReasonRequired => S["A reason is required."],
            LearningRefusals.NotActive => S["Only an active enrollment can be approved."],
            LearningRefusals.CourseUnavailable => S["The course is not available."],
            LearningRefusals.ItemNotFound => S["The course item was not found."],
            LearningRefusals.NoAccess => S["You do not have access to this course."],
            LearningRefusals.Busy => S["Someone else is changing this enrollment. Try again."],
            LearningRefusals.NotAvailableYet => S["This item is not available yet."],
            LearningRefusals.NotAvailable => S["This item is not available."],
            LearningRefusals.OpensLater => S["This item opens later."],
            LearningRefusals.NotInOutline => S["This item is not part of the course outline."],
            LearningRefusals.CompleteEarlierItem => S["Complete the earlier item first."],
            LearningRefusals.ReachEarlierScore => S["Reach the required score on the earlier item first."],
            LearningRefusals.NotMarkedDone => S["This item is not completed by marking it done."],
            LearningRefusals.NoStatement => S["This item has no statement to confirm."],
            LearningRefusals.ConfirmStatement => S["Tick the statement to confirm it."],
            _ => new LocalizedString(code, code, resourceNotFound: true),
        };
}
