using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Display driver for the AI chat session field extracted event shape.
/// </summary>
public sealed class AIChatSessionFieldExtractedEventDisplayDriver : ActivityDisplayDriver<AIChatSessionFieldExtractedEvent, AIChatSessionFieldExtractedEventViewModel>
{
    private readonly IAIProfileStore _profileStore;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionFieldExtractedEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The profile store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatSessionFieldExtractedEventDisplayDriver(
        IAIProfileStore profileStore,
        IStringLocalizer<AIChatSessionFieldExtractedEventDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        S = stringLocalizer;
    }

    protected override async ValueTask EditActivityAsync(AIChatSessionFieldExtractedEvent activity, AIChatSessionFieldExtractedEventViewModel model)
    {
        model.ProfileId = activity.ProfileId;
        model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Select(p => new SelectListItem(p.DisplayText, p.ItemId));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIChatSessionFieldExtractedEvent activity, BuildEditorContext context)
    {
        return Initialize<AIChatSessionFieldExtractedEventViewModel>(ActivityName + "_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Profile"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatSessionFieldExtractedEvent activity, UpdateEditorContext context)
    {
        var model = new AIChatSessionFieldExtractedEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.ProfileId = model.ProfileId;

        return Edit(activity, context);
    }
}
