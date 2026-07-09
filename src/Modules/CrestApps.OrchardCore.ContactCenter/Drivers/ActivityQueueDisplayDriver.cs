using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class ActivityQueueDisplayDriver : DisplayDriver<ActivityQueue>
{
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueDisplayDriver"/> class.
    /// </summary>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ActivityQueueDisplayDriver(
        ContactCenterAdminFormOptionsProvider optionsProvider,
        IStringLocalizer<ActivityQueueDisplayDriver> stringLocalizer)
    {
        _optionsProvider = optionsProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(ActivityQueue queue, BuildDisplayContext context)
    {
        return CombineAsync(
            View("ActivityQueue_Fields_SummaryAdmin", queue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("ActivityQueue_Buttons_SummaryAdmin", queue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("ActivityQueue_DefaultMeta_SummaryAdmin", queue)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(ActivityQueue queue, BuildEditorContext context)
    {
        var viewModel = new QueueViewModel
        {
            Id = queue.ItemId,
            Name = queue.Name,
            Description = queue.Description,
            DefaultPriority = queue.DefaultPriority,
            RoutingStrategy = queue.RoutingStrategy,
            PreferStickyAgent = queue.PreferStickyAgent,
            EnableSlaAging = queue.EnableSlaAging,
            SlaThresholdSeconds = queue.SlaThresholdSeconds,
            ReservationTimeoutSeconds = queue.ReservationTimeoutSeconds,
            UnansweredOfferAction = queue.UnansweredOfferAction,
            RequiredSkills = queue.RequiredSkills,
            InboundChannelEndpointId = queue.InboundChannelEndpointId,
            BusinessHoursCalendarId = queue.BusinessHoursCalendarId,
            AfterHoursAction = queue.AfterHoursAction,
            OverflowQueueId = queue.OverflowQueueId,
            OverflowAfterSeconds = queue.OverflowAfterSeconds,
            Enabled = queue.Enabled,
        };

        await _optionsProvider.PopulateQueueEditorAsync(viewModel);

        return Initialize<QueueViewModel>("ActivityQueueFields_Edit", model =>
        {
            model.Id = viewModel.Id;
            model.Name = viewModel.Name;
            model.Description = viewModel.Description;
            model.DefaultPriority = viewModel.DefaultPriority;
            model.RoutingStrategy = viewModel.RoutingStrategy;
            model.PreferStickyAgent = viewModel.PreferStickyAgent;
            model.EnableSlaAging = viewModel.EnableSlaAging;
            model.SlaThresholdSeconds = viewModel.SlaThresholdSeconds;
            model.ReservationTimeoutSeconds = viewModel.ReservationTimeoutSeconds;
            model.UnansweredOfferAction = viewModel.UnansweredOfferAction;
            model.RequiredSkills = viewModel.RequiredSkills;
            model.SkillOptions = viewModel.SkillOptions;
            model.InboundChannelEndpointId = viewModel.InboundChannelEndpointId;
            model.InboundChannelEndpointOptions = viewModel.InboundChannelEndpointOptions;
            model.BusinessHoursCalendarId = viewModel.BusinessHoursCalendarId;
            model.BusinessHoursCalendarOptions = viewModel.BusinessHoursCalendarOptions;
            model.AfterHoursAction = viewModel.AfterHoursAction;
            model.OverflowQueueId = viewModel.OverflowQueueId;
            model.OverflowQueueOptions = viewModel.OverflowQueueOptions;
            model.OverflowAfterSeconds = viewModel.OverflowAfterSeconds;
            model.Enabled = viewModel.Enabled;
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ActivityQueue queue, UpdateEditorContext context)
    {
        var model = new QueueViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is a required field."]);
        }

        queue.Name = model.Name?.Trim();
        queue.Description = model.Description?.Trim();
        queue.DefaultPriority = model.DefaultPriority;
        queue.RoutingStrategy = model.RoutingStrategy;
        queue.PreferStickyAgent = model.PreferStickyAgent;
        queue.EnableSlaAging = model.EnableSlaAging;
        queue.SlaThresholdSeconds = model.SlaThresholdSeconds;
        queue.ReservationTimeoutSeconds = model.ReservationTimeoutSeconds;
        queue.UnansweredOfferAction = model.UnansweredOfferAction;
        queue.RequiredSkills = ContactCenterFormHelpers.NormalizeList(model.RequiredSkills);
        queue.InboundChannelEndpointId = string.IsNullOrWhiteSpace(model.InboundChannelEndpointId)
            ? null
            : model.InboundChannelEndpointId.Trim();
        queue.BusinessHoursCalendarId = string.IsNullOrWhiteSpace(model.BusinessHoursCalendarId)
            ? null
            : model.BusinessHoursCalendarId.Trim();
        queue.AfterHoursAction = model.AfterHoursAction;
        queue.OverflowQueueId = string.IsNullOrWhiteSpace(model.OverflowQueueId) || string.Equals(model.OverflowQueueId, queue.ItemId, StringComparison.Ordinal)
            ? null
            : model.OverflowQueueId.Trim();
        queue.OverflowAfterSeconds = Math.Max(0, model.OverflowAfterSeconds);
        queue.Enabled = model.Enabled;

        return await EditAsync(queue, context);
    }
}
