using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="CreateAIActivityTask"/> workflow activity.
/// </summary>
public sealed class CreateAIActivityTaskDisplayDriver : ActivityDisplayDriver<CreateAIActivityTask, CreateAIActivityTaskViewModel>
{
    private readonly IAIProfileStore _profileStore;
    private readonly IOmnichannelChannelEndpointStore _endpointStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAIActivityTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <param name="endpointStore">The channel endpoint store, which lists your numbers.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager, which validates the expressions.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CreateAIActivityTaskDisplayDriver(
        IAIProfileStore profileStore,
        IOmnichannelChannelEndpointStore endpointStore,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<CreateAIActivityTaskDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        _endpointStore = endpointStore;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(CreateAIActivityTask activity, CreateAIActivityTaskViewModel model)
    {
        model.Channel = activity.Channel;
        model.AIProfileId = activity.AIProfileId;
        model.ChannelEndpointId = activity.ChannelEndpointId;
        model.Destination = activity.Destination;
        model.ContactContentItemId = activity.ContactContentItemId;
        model.SubjectContentType = activity.SubjectContentType;
        model.CampaignId = activity.CampaignId;
        model.Variables = activity.Variables;
        model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Select(profile => new SelectListItem(profile.DisplayText, profile.ItemId));
        model.Endpoints = (await _endpointStore.GetAllAsync())
            .Where(endpoint => endpoint.Channel is OmnichannelConstants.Channels.Phone or OmnichannelConstants.Channels.Sms)
            .OrderBy(endpoint => endpoint.DisplayText)
            .Select(endpoint => new SelectListItem($"{endpoint.DisplayText} ({endpoint.Value})", endpoint.ItemId));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(CreateAIActivityTask activity, UpdateEditorContext context)
    {
        var model = new CreateAIActivityTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.AIProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileId), S["Choose the AI profile that holds the conversation."]);
        }

        if (string.IsNullOrWhiteSpace(model.Destination))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Destination), S["The customer's phone number is required."]);
        }

        Validate(model.Destination, nameof(model.Destination), context);
        Validate(model.ContactContentItemId, nameof(model.ContactContentItemId), context);
        Validate(model.Variables, nameof(model.Variables), context);

        activity.Channel = string.IsNullOrWhiteSpace(model.Channel) ? OmnichannelConstants.Channels.Phone : model.Channel;
        activity.AIProfileId = model.AIProfileId;
        activity.ChannelEndpointId = model.ChannelEndpointId;
        activity.Destination = model.Destination?.Trim();
        activity.ContactContentItemId = model.ContactContentItemId?.Trim();
        activity.SubjectContentType = model.SubjectContentType?.Trim();
        activity.CampaignId = model.CampaignId?.Trim();
        activity.Variables = model.Variables;

        return Edit(activity, context);
    }

    private void Validate(string value, string member, UpdateEditorContext context)
    {
        if (!string.IsNullOrWhiteSpace(value) && !_liquidTemplateManager.Validate(value, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, member, S["The expression is invalid."]);
        }
    }
}
