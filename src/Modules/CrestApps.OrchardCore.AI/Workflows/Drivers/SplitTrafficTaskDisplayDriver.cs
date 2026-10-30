using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Edits the Split Traffic task.
/// </summary>
public sealed class SplitTrafficTaskDisplayDriver : ActivityDisplayDriver<SplitTrafficTask, SplitTrafficTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SplitTrafficTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager, which validates the templates.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SplitTrafficTaskDisplayDriver(ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<SplitTrafficTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(SplitTrafficTask activity, SplitTrafficTaskViewModel model)
    {

        model.Variants = activity.Variants;
        model.StickyKey = activity.StickyKey;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(SplitTrafficTask activity, UpdateEditorContext context)
    {
        var model = new SplitTrafficTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);


        if (SplitTrafficTask.ParseVariants(model.Variants).Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Variants), S["Add at least one outcome with a weight above zero, such as A: 50."]);
        }

        if (!string.IsNullOrWhiteSpace(model.StickyKey) && !_liquidTemplateManager.Validate(model.StickyKey, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.StickyKey), S["The key is not a valid Liquid template."]);
        }

        activity.Variants = model.Variants;
        activity.StickyKey = model.StickyKey?.Trim();

        return Edit(activity, context);
    }
}
