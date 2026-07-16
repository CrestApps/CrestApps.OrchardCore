using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class DialerProfileDisplayDriver : DisplayDriver<DialerProfile>
{
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileDisplayDriver"/> class.
    /// </summary>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="shellFeaturesManager">The shell features manager used to detect the Automated Dialer feature.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerProfileDisplayDriver(
        ContactCenterAdminFormOptionsProvider optionsProvider,
        IShellFeaturesManager shellFeaturesManager,
        IStringLocalizer<DialerProfileDisplayDriver> stringLocalizer)
    {
        _optionsProvider = optionsProvider;
        _shellFeaturesManager = shellFeaturesManager;
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
            EnforceCallingWindow = profile.EnforceCallingWindow,
            CallingCalendarId = profile.CallingCalendarId,
            EnforceAbandonmentCap = profile.EnforceAbandonmentCap,
            MaxAbandonmentRatePercent = profile.MaxAbandonmentRatePercent,
            AbandonmentSampleFloor = profile.AbandonmentSampleFloor,
            SafeHarborEnabled = profile.SafeHarborEnabled,
            SafeHarborMessage = profile.SafeHarborMessage,
            Enabled = profile.Enabled,
        };

        await _optionsProvider.PopulateDialerProfileEditorAsync(viewModel);

        var automatedDialerEnabled = await _shellFeaturesManager.IsFeatureEnabledAsync(ContactCenterConstants.Feature.DialerAutomated);

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
            model.AutomatedDialerEnabled = automatedDialerEnabled;
            model.ProviderName = viewModel.ProviderName;
            model.ProviderOptions = viewModel.ProviderOptions;
            model.CallsPerAgent = viewModel.CallsPerAgent;
            model.MaxAttempts = viewModel.MaxAttempts;
            model.RetryDelayMinutes = viewModel.RetryDelayMinutes;
            model.CallerId = viewModel.CallerId;
            model.RespectDoNotCall = viewModel.RespectDoNotCall;
            model.EnforceCallingWindow = viewModel.EnforceCallingWindow;
            model.CallingCalendarId = viewModel.CallingCalendarId;
            model.CallingCalendarOptions = viewModel.CallingCalendarOptions;
            model.EnforceAbandonmentCap = viewModel.EnforceAbandonmentCap;
            model.MaxAbandonmentRatePercent = viewModel.MaxAbandonmentRatePercent;
            model.AbandonmentSampleFloor = viewModel.AbandonmentSampleFloor;
            model.SafeHarborEnabled = viewModel.SafeHarborEnabled;
            model.SafeHarborMessage = viewModel.SafeHarborMessage;
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

        if (model.Mode == DialerMode.Predictive)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Mode), S["Predictive dialing is not available yet. Choose Manual, Preview, Power, or Progressive."]);
        }
        else if (model.Mode.RequiresAutomatedDialerFeature() &&
            !await _shellFeaturesManager.IsFeatureEnabledAsync(ContactCenterConstants.Feature.DialerAutomated))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Mode), S["Enable the Contact Center Automated Dialer feature before using Power or Progressive dialing."]);
        }

        if (model.EnforceCallingWindow && string.IsNullOrWhiteSpace(model.CallingCalendarId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CallingCalendarId), S["Select an outbound calling calendar when calling-window enforcement is enabled."]);
        }

        if (model.MaxAbandonmentRatePercent is < 0 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxAbandonmentRatePercent), S["The maximum abandonment rate must be between 0 and 100 percent."]);
        }

        if (model.AbandonmentSampleFloor < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AbandonmentSampleFloor), S["The abandonment sample floor cannot be negative."]);
        }

        var automatedMode = model.Mode.IsAutomated();

        if (model.EnforceAbandonmentCap && automatedMode && !model.SafeHarborEnabled)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SafeHarborEnabled), S["Enable safe-harbor messaging when an automated dialing mode enforces an abandonment cap."]);
        }

        if (model.SafeHarborEnabled && string.IsNullOrWhiteSpace(model.SafeHarborMessage))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SafeHarborMessage), S["Provide a safe-harbor announcement when safe-harbor messaging is enabled."]);
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
        profile.CallsPerAgent = Math.Clamp(model.CallsPerAgent, 1, PowerDialerStrategy.MaxCallsPerAgent);
        profile.MaxAttempts = model.MaxAttempts;
        profile.RetryDelayMinutes = model.RetryDelayMinutes;
        profile.CallerId = model.CallerId?.Trim();
        profile.RespectDoNotCall = model.RespectDoNotCall;
        profile.EnforceCallingWindow = model.EnforceCallingWindow;
        profile.CallingCalendarId = string.IsNullOrWhiteSpace(model.CallingCalendarId)
            ? null
            : model.CallingCalendarId.Trim();
        profile.EnforceAbandonmentCap = model.EnforceAbandonmentCap;
        profile.MaxAbandonmentRatePercent = Math.Clamp(model.MaxAbandonmentRatePercent, 0, 100);
        profile.AbandonmentSampleFloor = Math.Max(0, model.AbandonmentSampleFloor);
        profile.SafeHarborEnabled = model.SafeHarborEnabled;
        profile.SafeHarborMessage = string.IsNullOrWhiteSpace(model.SafeHarborMessage)
            ? null
            : model.SafeHarborMessage.Trim();
        profile.Enabled = model.Enabled;

        return await EditAsync(profile, context);
    }
}
