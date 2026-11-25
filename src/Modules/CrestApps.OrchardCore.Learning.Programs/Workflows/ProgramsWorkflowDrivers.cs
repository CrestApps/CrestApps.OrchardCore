using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Programs.Workflows;

public class RequiredLearningEventViewModel
{
    public string[] RequirementIds { get; set; } = [];

    [BindNever]
    public List<SelectListItem> RequirementOptions { get; set; } = [];
}

public class LearningPathCompletedEventViewModel
{
    public string[] PathIds { get; set; } = [];

    [BindNever]
    public List<SelectListItem> PathOptions { get; set; } = [];
}

public class RequireLearningTaskViewModel
{
    public string RequirementId { get; set; }

    public string UserId { get; set; }

    public string Reason { get; set; }

    [BindNever]
    public List<SelectListItem> RequirementOptions { get; set; } = [];
}

public sealed class RequiredLearningEventDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, RequiredLearningEventViewModel>
    where TActivity : RequiredLearningEventActivity
{
    private readonly IRequiredLearningService _requiredLearning;

    internal readonly IStringLocalizer S;

    public RequiredLearningEventDisplayDriver(
        IRequiredLearningService requiredLearning,
        IStringLocalizer<RequiredLearningEventDisplayDriver<TActivity>> stringLocalizer)
    {
        _requiredLearning = requiredLearning;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
    {
        return Initialize<RequiredLearningEventViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Trigger"], "10"));
    }

    protected override async ValueTask EditActivityAsync(TActivity activity, RequiredLearningEventViewModel model)
    {
        model.RequirementIds = activity.RequirementIds.ToArray();
        model.RequirementOptions = (await _requiredLearning.ListAsync())
            .OrderBy(rule => rule.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(rule => new SelectListItem(rule.Name, rule.ItemId, model.RequirementIds.Contains(rule.ItemId)))
            .ToList();
    }

    protected override void UpdateActivity(RequiredLearningEventViewModel model, TActivity activity)
        => activity.RequirementIds = (model.RequirementIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
}

public sealed class LearningPathCompletedEventDisplayDriver : ActivityDisplayDriver<LearningPathCompletedEvent, LearningPathCompletedEventViewModel>
{
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    public LearningPathCompletedEventDisplayDriver(
        ISession session,
        IStringLocalizer<LearningPathCompletedEventDisplayDriver> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(LearningPathCompletedEvent activity, BuildEditorContext context)
    {
        return Initialize<LearningPathCompletedEventViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Trigger"], "10"));
    }

    protected override async ValueTask EditActivityAsync(LearningPathCompletedEvent activity, LearningPathCompletedEventViewModel model)
    {
        model.PathIds = activity.PathIds.ToArray();
        model.PathOptions = (await _session.QueryIndex<ContentItemIndex>(index => index.ContentType == ProgramsConstants.LearningPathContentType && index.Latest)
                .OrderBy(index => index.DisplayText)
                .ListAsync())
            .Select(index => new SelectListItem(index.DisplayText, index.ContentItemId, model.PathIds.Contains(index.ContentItemId)))
            .ToList();
    }

    protected override void UpdateActivity(LearningPathCompletedEventViewModel model, LearningPathCompletedEvent activity)
        => activity.PathIds = (model.PathIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
}

public sealed class RequireLearningTaskDisplayDriver : ActivityDisplayDriver<RequireLearningTask, RequireLearningTaskViewModel>
{
    private readonly IRequiredLearningService _requiredLearning;

    internal readonly IStringLocalizer S;

    public RequireLearningTaskDisplayDriver(
        IRequiredLearningService requiredLearning,
        IStringLocalizer<RequireLearningTaskDisplayDriver> stringLocalizer)
    {
        _requiredLearning = requiredLearning;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(RequireLearningTask activity, BuildEditorContext context)
    {
        return Initialize<RequireLearningTaskViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Task"], "10"));
    }

    protected override async ValueTask EditActivityAsync(RequireLearningTask activity, RequireLearningTaskViewModel model)
    {
        model.RequirementId = activity.RequirementId;
        model.UserId = activity.UserId;
        model.RequirementOptions = await OptionsAsync(_requiredLearning, activity.RequirementId);
    }

    protected override void UpdateActivity(RequireLearningTaskViewModel model, RequireLearningTask activity)
    {
        activity.RequirementId = model.RequirementId?.Trim();
        activity.UserId = model.UserId?.Trim();
    }

    internal static async Task<List<SelectListItem>> OptionsAsync(IRequiredLearningService requiredLearning, string selected)
        => (await requiredLearning.ListAsync())
            .OrderBy(rule => rule.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(rule => new SelectListItem(rule.Name, rule.ItemId, rule.ItemId == selected))
            .ToList();
}

public sealed class GrantRequiredLearningExemptionTaskDisplayDriver : ActivityDisplayDriver<GrantRequiredLearningExemptionTask, RequireLearningTaskViewModel>
{
    private readonly IRequiredLearningService _requiredLearning;

    internal readonly IStringLocalizer S;

    public GrantRequiredLearningExemptionTaskDisplayDriver(
        IRequiredLearningService requiredLearning,
        IStringLocalizer<GrantRequiredLearningExemptionTaskDisplayDriver> stringLocalizer)
    {
        _requiredLearning = requiredLearning;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(GrantRequiredLearningExemptionTask activity, BuildEditorContext context)
    {
        return Initialize<RequireLearningTaskViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Task"], "10"));
    }

    protected override async ValueTask EditActivityAsync(GrantRequiredLearningExemptionTask activity, RequireLearningTaskViewModel model)
    {
        model.RequirementId = activity.RequirementId;
        model.UserId = activity.UserId;
        model.Reason = activity.Reason;
        model.RequirementOptions = await RequireLearningTaskDisplayDriver.OptionsAsync(_requiredLearning, activity.RequirementId);
    }

    protected override void UpdateActivity(RequireLearningTaskViewModel model, GrantRequiredLearningExemptionTask activity)
    {
        activity.RequirementId = model.RequirementId?.Trim();
        activity.UserId = model.UserId?.Trim();
        activity.Reason = model.Reason?.Trim();
    }
}
