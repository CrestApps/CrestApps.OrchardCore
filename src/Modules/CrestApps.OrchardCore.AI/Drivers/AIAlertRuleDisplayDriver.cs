using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Displays an AI alert rule in the admin list and edits it.
/// </summary>
public sealed class AIAlertRuleDisplayDriver : DisplayDriver<AIAlertRule>
{
    private readonly IAIProfileStore _profileStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIAlertRuleDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIAlertRuleDisplayDriver(IAIProfileStore profileStore, IStringLocalizer<AIAlertRuleDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(AIAlertRule rule, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("AIAlertRule_Fields_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AIAlertRule_DefaultTags_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("AIAlertRule_Description_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("AIAlertRule_Buttons_SummaryAdmin", rule).Location("Actions:5"),
            View("AIAlertRule_ActionsMenu_SummaryAdmin", rule).Location("ActionsMenu:10"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIAlertRule rule, BuildEditorContext context)
    {
        return Initialize<AIAlertRuleViewModel>("AIAlertRuleFields_Edit", async model =>
        {
            model.Name = rule.Name;
            model.Enabled = rule.Enabled;
            model.Metric = rule.Metric;
            model.ProfileId = rule.ProfileId;
            model.Condition = rule.Condition;
            model.Threshold = rule.Threshold;
            model.WindowMinutes = rule.WindowMinutes;
            model.MinimumSampleSize = rule.MinimumSampleSize;
            model.CheckEveryMinutes = rule.CheckEveryMinutes;
            model.NotifyEmails = rule.NotifyEmails;
            model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
                .Where(profile => !profile.Has<AIProfileVersionCopy>())
                .OrderBy(profile => profile.DisplayText, StringComparer.CurrentCultureIgnoreCase)
                .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId))
                .ToList();
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIAlertRule rule, UpdateEditorContext context)
    {
        var model = new AIAlertRuleViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The name is required."]);
        }

        if (model.WindowMinutes is < 1 or > 43200)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.WindowMinutes), S["The window must be between 1 minute and 30 days."]);
        }

        if (model.CheckEveryMinutes is < 1 or > 1440)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CheckEveryMinutes), S["Check between every minute and once a day."]);
        }

        if (model.MinimumSampleSize < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MinimumSampleSize), S["The minimum sample cannot be negative."]);
        }

        var changed = rule.Metric != model.Metric || rule.Condition != model.Condition || rule.Threshold != model.Threshold || rule.ProfileId != model.ProfileId;

        rule.Name = model.Name?.Trim();
        rule.Enabled = model.Enabled;
        rule.Metric = model.Metric;
        rule.ProfileId = string.IsNullOrWhiteSpace(model.ProfileId) ? null : model.ProfileId;
        rule.Condition = model.Condition;
        rule.Threshold = model.Threshold;
        rule.WindowMinutes = model.WindowMinutes;
        rule.MinimumSampleSize = model.MinimumSampleSize;
        rule.CheckEveryMinutes = model.CheckEveryMinutes;
        rule.NotifyEmails = model.NotifyEmails?.Trim();

        // A changed rule is checked at once.
        if (changed)
        {
            rule.LastCheckedUtc = null;
        }

        return Edit(rule, context);
    }
}
