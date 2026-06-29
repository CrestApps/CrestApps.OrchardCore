using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class DialerProfileDisplayDriver : DisplayDriver<DialerProfile>
{
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileDisplayDriver"/> class.
    /// </summary>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerProfileDisplayDriver(
        ContactCenterAdminFormOptionsProvider optionsProvider,
        IStringLocalizer<DialerProfileDisplayDriver> stringLocalizer)
    {
        _optionsProvider = optionsProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(DialerProfile profile, BuildDisplayContext context)
    {
        return CombineAsync(
            View("DialerProfile_Fields_SummaryAdmin", profile)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("DialerProfile_Buttons_SummaryAdmin", profile)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("DialerProfile_DefaultMeta_SummaryAdmin", profile)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(DialerProfile profile, BuildEditorContext context)
    {
        var viewModel = new DialerProfileViewModel
        {
            Id = profile.ItemId,
            Name = profile.Name,
            Description = profile.Description,
            CampaignId = profile.CampaignId,
            QueueId = profile.QueueId,
            Mode = profile.Mode,
            ProviderName = profile.ProviderName,
            CallsPerAgent = profile.CallsPerAgent,
            MaxAttempts = profile.MaxAttempts,
            RetryDelayMinutes = profile.RetryDelayMinutes,
            CallerId = profile.CallerId,
            RespectDoNotCall = profile.RespectDoNotCall,
            Enabled = profile.Enabled,
        };

        await _optionsProvider.PopulateDialerProfileEditorAsync(viewModel);

        return Initialize<DialerProfileViewModel>("DialerProfileFields_Edit", model =>
        {
            model.Id = viewModel.Id;
            model.Name = viewModel.Name;
            model.Description = viewModel.Description;
            model.CampaignId = viewModel.CampaignId;
            model.CampaignOptions = viewModel.CampaignOptions;
            model.QueueId = viewModel.QueueId;
            model.QueueOptions = viewModel.QueueOptions;
            model.Mode = viewModel.Mode;
            model.ProviderName = viewModel.ProviderName;
            model.ProviderOptions = viewModel.ProviderOptions;
            model.CallsPerAgent = viewModel.CallsPerAgent;
            model.MaxAttempts = viewModel.MaxAttempts;
            model.RetryDelayMinutes = viewModel.RetryDelayMinutes;
            model.CallerId = viewModel.CallerId;
            model.RespectDoNotCall = viewModel.RespectDoNotCall;
            model.Enabled = viewModel.Enabled;
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(DialerProfile profile, UpdateEditorContext context)
    {
        var model = new DialerProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is a required field."]);
        }

        profile.Name = model.Name?.Trim();
        profile.Description = model.Description?.Trim();
        profile.CampaignId = string.IsNullOrWhiteSpace(model.CampaignId)
            ? null
            : model.CampaignId.Trim();
        profile.QueueId = string.IsNullOrWhiteSpace(model.QueueId)
            ? null
            : model.QueueId.Trim();
        profile.Mode = model.Mode;
        profile.ProviderName = string.IsNullOrWhiteSpace(model.ProviderName)
            ? null
            : model.ProviderName.Trim();
        profile.CallsPerAgent = model.CallsPerAgent;
        profile.MaxAttempts = model.MaxAttempts;
        profile.RetryDelayMinutes = model.RetryDelayMinutes;
        profile.CallerId = model.CallerId?.Trim();
        profile.RespectDoNotCall = model.RespectDoNotCall;
        profile.Enabled = model.Enabled;

        return await EditAsync(profile, context);
    }
}
