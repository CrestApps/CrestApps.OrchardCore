using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Edits the guardrails of an AI profile: the topics it refuses and what it says instead.
/// </summary>
public sealed class AIProfileGuardrailDisplayDriver : DisplayDriver<AIProfile>
{
    private const int MaxPlaceholderLength = 500;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileGuardrailDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileGuardrailDisplayDriver(IStringLocalizer<AIProfileGuardrailDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileGuardrailViewModel>("AIProfileGuardrail_Edit", model =>
        {
            var settings = profile.GetSettings<AIProfileGuardrailSettings>();
            model.CheckCustomer = settings.CheckCustomer;
            model.CheckReplies = settings.CheckReplies;
            model.Categories = AIGuardrailCategories.Normalize(settings.Categories);
            model.OtherTopics = settings.OtherTopics;
            model.PlaceholderReply = settings.PlaceholderReply;
        }).Location("Content:5%Prompt Security;100");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileGuardrailViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var categories = AIGuardrailCategories.Normalize(model.Categories);

        if ((model.CheckCustomer || model.CheckReplies) && categories.Length == 0 && string.IsNullOrWhiteSpace(model.OtherTopics))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Categories), S["Choose at least one category, or describe the other topics to refuse."]);
        }

        if (model.PlaceholderReply?.Length > MaxPlaceholderLength)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PlaceholderReply), S["The placeholder reply can't be longer than {0} characters.", MaxPlaceholderLength]);
        }

        profile.AlterSettings<AIProfileGuardrailSettings>(settings =>
        {
            settings.CheckCustomer = model.CheckCustomer;
            settings.CheckReplies = model.CheckReplies;
            settings.Categories = categories;
            settings.OtherTopics = model.OtherTopics?.Trim();
            settings.PlaceholderReply = model.PlaceholderReply?.Trim();
        });

        return Edit(profile, context);
    }
}
