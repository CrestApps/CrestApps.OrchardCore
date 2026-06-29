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
            SlaThresholdSeconds = queue.SlaThresholdSeconds,
            ReservationTimeoutSeconds = queue.ReservationTimeoutSeconds,
            RequiredSkills = queue.RequiredSkills,
            InboundChannelEndpointId = queue.InboundChannelEndpointId,
            Enabled = queue.Enabled,
        };

        await _optionsProvider.PopulateQueueEditorAsync(viewModel);

        return Initialize<QueueViewModel>("ActivityQueueFields_Edit", model =>
        {
            model.Id = viewModel.Id;
            model.Name = viewModel.Name;
            model.Description = viewModel.Description;
            model.DefaultPriority = viewModel.DefaultPriority;
            model.SlaThresholdSeconds = viewModel.SlaThresholdSeconds;
            model.ReservationTimeoutSeconds = viewModel.ReservationTimeoutSeconds;
            model.RequiredSkills = viewModel.RequiredSkills;
            model.SkillOptions = viewModel.SkillOptions;
            model.InboundChannelEndpointId = viewModel.InboundChannelEndpointId;
            model.InboundChannelEndpointOptions = viewModel.InboundChannelEndpointOptions;
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
        queue.SlaThresholdSeconds = model.SlaThresholdSeconds;
        queue.ReservationTimeoutSeconds = model.ReservationTimeoutSeconds;
        queue.RequiredSkills = ContactCenterFormHelpers.NormalizeList(model.RequiredSkills);
        queue.InboundChannelEndpointId = string.IsNullOrWhiteSpace(model.InboundChannelEndpointId)
            ? null
            : model.InboundChannelEndpointId.Trim();
        queue.Enabled = model.Enabled;

        return await EditAsync(queue, context);
    }
}
