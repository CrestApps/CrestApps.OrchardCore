using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Drivers;

/// <summary>
/// Edits the Find Agent Numbers workflow task.
/// </summary>
public sealed class FindAgentNumbersTaskDisplayDriver : ActivityDisplayDriver<FindAgentNumbersTask, FindAgentNumbersTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FindAgentNumbersTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager, to validate the expression.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FindAgentNumbersTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<FindAgentNumbersTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(FindAgentNumbersTask activity, FindAgentNumbersTaskViewModel model)
    {
        model.UserName = activity.UserName;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(FindAgentNumbersTask activity, UpdateEditorContext context)
    {
        var model = new FindAgentNumbersTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Empty is allowed: the task then returns the default numbers.
        if (!string.IsNullOrWhiteSpace(model.UserName) && !_liquidTemplateManager.Validate(model.UserName, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserName), S["The User name expression is invalid."]);
        }

        activity.UserName = model.UserName?.Trim();

        return Edit(activity, context);
    }
}
