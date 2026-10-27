using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
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

    private readonly ITemplateService _templateService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileGuardrailDisplayDriver"/> class.
    /// </summary>
    /// <param name="templateService">The template service the topics are listed from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileGuardrailDisplayDriver(
        ITemplateService templateService,
        IStringLocalizer<AIProfileGuardrailDisplayDriver> stringLocalizer)
    {
        _templateService = templateService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileGuardrailViewModel>("AIProfileGuardrail_Edit", async model =>
        {
            var settings = profile.GetSettings<AIProfileGuardrailSettings>();
            model.CheckCustomer = settings.CheckCustomer;
            model.CheckReplies = settings.CheckReplies;
            model.Topics = AIGuardrailTopics.Normalize(settings.Topics);
            model.AvailableTopics = await _templateService.ListOptionsByCategoryAsync(AIGuardrailTopics.Category);
            model.OtherTopics = settings.OtherTopics;
            model.PlaceholderReply = settings.PlaceholderReply;
        }).Location("Content:1%Guardrails;100.5");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileGuardrailViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var topics = AIGuardrailTopics.Normalize(model.Topics);

        if ((model.CheckCustomer || model.CheckReplies) && topics.Length == 0 && string.IsNullOrWhiteSpace(model.OtherTopics))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Topics), S["Choose at least one topic, or describe the other topics to refuse."]);
        }

        if (model.PlaceholderReply?.Length > MaxPlaceholderLength)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PlaceholderReply), S["The placeholder reply can't be longer than {0} characters.", MaxPlaceholderLength]);
        }

        profile.AlterSettings<AIProfileGuardrailSettings>(settings =>
        {
            settings.CheckCustomer = model.CheckCustomer;
            settings.CheckReplies = model.CheckReplies;
            settings.Topics = topics;
            settings.OtherTopics = model.OtherTopics?.Trim();
            settings.PlaceholderReply = model.PlaceholderReply?.Trim();
        });

        return Edit(profile, context);
    }
}
