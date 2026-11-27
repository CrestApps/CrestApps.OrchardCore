using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

/// <summary>
/// The editor of the form that gives a learner a quiz override: the learner, the extra time, attempts and window, and why.
/// </summary>
internal sealed class QuizOverridesDisplayDriver : DisplayDriver<QuizOverridesViewModel>
{
    internal readonly IStringLocalizer S;

    public QuizOverridesDisplayDriver(IStringLocalizer<QuizOverridesDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(QuizOverridesViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<QuizOverridesViewModel>("QuizOverrideLearner_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Learner"], "10")),
            Initialize<QuizOverridesViewModel>("QuizOverrideLimits_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Time and Attempts"], "20")),
            Initialize<QuizOverridesViewModel>("QuizOverrideReason_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Reason"], "30")));
    }

    public override async Task<IDisplayResult> UpdateAsync(QuizOverridesViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.UserName,
            m => m.ExtraMinutes,
            m => m.ExtraAttempts,
            m => m.Opens,
            m => m.Closes,
            m => m.Reason);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(QuizOverridesViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(QuizOverridesViewModel source, QuizOverridesViewModel model)
    {
        model.UserName = source.UserName;
        model.ExtraMinutes = source.ExtraMinutes;
        model.ExtraAttempts = source.ExtraAttempts;
        model.Opens = source.Opens;
        model.Closes = source.Closes;
        model.Reason = source.Reason;
    }
}
