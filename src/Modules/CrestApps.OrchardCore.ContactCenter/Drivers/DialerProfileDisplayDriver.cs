using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
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
    private readonly IEnumerable<IDialerAbandonmentStatisticsProvider> _statisticsProviders;
    private readonly ContactCenterComplianceOptions _complianceOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileDisplayDriver"/> class.
    /// </summary>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="shellFeaturesManager">The shell features manager used to detect the Paced Dialing feature.</param>
    /// <param name="statisticsProviders">The providers of the measured abandonment shown on the editor.</param>
    /// <param name="complianceOptions">The compliance options, for the rolling window the abandonment cap is measured over.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerProfileDisplayDriver(
        ContactCenterAdminFormOptionsProvider optionsProvider,
        IShellFeaturesManager shellFeaturesManager,
        IEnumerable<IDialerAbandonmentStatisticsProvider> statisticsProviders,
        IOptions<ContactCenterComplianceOptions> complianceOptions,
        IStringLocalizer<DialerProfileDisplayDriver> stringLocalizer)
    {
        _optionsProvider = optionsProvider;
        _shellFeaturesManager = shellFeaturesManager;
        _statisticsProviders = statisticsProviders;
        _complianceOptions = complianceOptions.Value;
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
            // Manual is no longer offered as a dialer-profile mode (agent free-dialing is governed by the separate
            // manual-dialing compliance policy). Any legacy Manual profile is shown and re-saved as the equivalent
            // agent-initiated Preview mode.
            Mode = profile.Mode == DialerMode.Manual ? DialerMode.Preview : profile.Mode,
            ProviderName = profile.ProviderName,
            CallsPerAgent = profile.CallsPerAgent,
            MaxAttempts = profile.MaxAttempts,
            RetryDelayMinutes = profile.RetryDelayMinutes,
            AnsweringMachineDetection = profile.AnsweringMachineDetection,
            RingTimeoutSeconds = profile.RingTimeoutSeconds > 0 ? profile.RingTimeoutSeconds : DialerAbandonment.DefaultRingTimeoutSeconds,
            CallerId = profile.CallerId,
            AlwaysUseCallerId = profile.AlwaysUseCallerId,
            DefaultRegionCode = profile.DefaultRegionCode,
            RespectDoNotCall = profile.RespectDoNotCall,
            EnforceCallingWindow = profile.EnforceCallingWindow,
            CallingCalendarId = profile.CallingCalendarId,
            EnforceAbandonmentCap = profile.EnforceAbandonmentCap,
            MaxAbandonmentRatePercent = profile.MaxAbandonmentRatePercent,
            AbandonmentSampleFloor = profile.AbandonmentSampleFloor,
            SafeHarborEnabled = profile.SafeHarborEnabled,
            SafeHarborMessage = profile.SafeHarborMessage,
            Enabled = profile.Enabled,
            AbandonmentWindowMinutes = _complianceOptions.AbandonmentRollingWindowMinutes,
        };

        await _optionsProvider.PopulateDialerProfileEditorAsync(viewModel);

        // What the profile has actually measured, so whoever sets the cap sees the rate it is held to. A new profile
        // has nothing to measure.
        if (!string.IsNullOrEmpty(profile.ItemId) && profile.Mode.IsAutomated())
        {
            viewModel.RollingAbandonment = await GetStatisticsAsync(profile.ItemId, TimeSpan.FromMinutes(_complianceOptions.AbandonmentRollingWindowMinutes));
            viewModel.MonthlyAbandonment = await GetStatisticsAsync(profile.ItemId, TimeSpan.FromDays(30));
        }

        var automatedDialerEnabled = await _shellFeaturesManager.IsFeatureEnabledAsync(ContactCenterConstants.Feature.DialerPaced);

        // Grouped in cards by what they govern. Every card edits the same model under the same prefix, so the one form
        // still posts all of them together.
        void Populate(DialerProfileViewModel model)
        {
            model.Id = viewModel.Id;
            model.Name = viewModel.Name;
            model.Description = viewModel.Description;
            model.Mode = viewModel.Mode;
            model.AutomatedDialerEnabled = automatedDialerEnabled;
            model.ProviderName = viewModel.ProviderName;
            model.ProviderOptions = viewModel.ProviderOptions;
            model.CallsPerAgent = viewModel.CallsPerAgent;
            model.MaxAttempts = viewModel.MaxAttempts;
            model.RetryDelayMinutes = viewModel.RetryDelayMinutes;
            model.AnsweringMachineDetection = viewModel.AnsweringMachineDetection;
            model.RingTimeoutSeconds = viewModel.RingTimeoutSeconds;
            model.CallerId = viewModel.CallerId;
            model.AlwaysUseCallerId = viewModel.AlwaysUseCallerId;
            model.DefaultRegionCode = viewModel.DefaultRegionCode;
            model.DefaultRegionOptions = viewModel.DefaultRegionOptions;
            model.CallerIdOptions = viewModel.CallerIdOptions;
            model.RespectDoNotCall = viewModel.RespectDoNotCall;
            model.EnforceCallingWindow = viewModel.EnforceCallingWindow;
            model.CallingCalendarId = viewModel.CallingCalendarId;
            model.CallingCalendarOptions = viewModel.CallingCalendarOptions;
            model.EnforceAbandonmentCap = viewModel.EnforceAbandonmentCap;
            model.MaxAbandonmentRatePercent = viewModel.MaxAbandonmentRatePercent;
            model.AbandonmentSampleFloor = viewModel.AbandonmentSampleFloor;
            model.SafeHarborEnabled = viewModel.SafeHarborEnabled;
            model.SafeHarborMessage = viewModel.SafeHarborMessage;
            model.RollingAbandonment = viewModel.RollingAbandonment;
            model.AbandonmentWindowMinutes = viewModel.AbandonmentWindowMinutes;
            model.MonthlyAbandonment = viewModel.MonthlyAbandonment;
            model.Enabled = viewModel.Enabled;
        }

        return Combine(
            Initialize<DialerProfileViewModel>("DialerProfileGeneral_Edit", Populate).Location("Content:1%General;1"),
            Initialize<DialerProfileViewModel>("DialerProfileDialing_Edit", Populate).Location("Content:1%Dialing;2"),
            Initialize<DialerProfileViewModel>("DialerProfileCallerId_Edit", Populate).Location("Content:1%Caller ID;3"),
            Initialize<DialerProfileViewModel>("DialerProfileCompliance_Edit", Populate).Location("Content:1%Compliance;4"),
            Initialize<DialerProfileViewModel>("DialerProfileAbandonment_Edit", Populate).Location("Content:1%Abandoned calls;5"));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(DialerProfile profile, UpdateEditorContext context)
    {
        var model = new DialerProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.Name = model.Name?.Trim();
        profile.Description = model.Description?.Trim();

        // A dialer profile is reusable settings: it no longer owns a campaign or queue. The campaign is chosen
        // when inventory is loaded (on the activity batch), which is also where the profile is selected.
        profile.Mode = model.Mode;
        profile.ProviderName = string.IsNullOrWhiteSpace(model.ProviderName)
            ? null
            : model.ProviderName.Trim();
        profile.CallsPerAgent = model.CallsPerAgent;
        profile.MaxAttempts = model.MaxAttempts;
        profile.RetryDelayMinutes = model.RetryDelayMinutes;
        profile.AnsweringMachineDetection = model.AnsweringMachineDetection;
        profile.RingTimeoutSeconds = model.RingTimeoutSeconds;
        profile.CallerId = model.CallerId?.Trim();
        profile.AlwaysUseCallerId = model.AlwaysUseCallerId;
        profile.DefaultRegionCode = model.DefaultRegionCode?.Trim().ToUpperInvariant();
        profile.RespectDoNotCall = model.RespectDoNotCall;
        profile.EnforceCallingWindow = model.EnforceCallingWindow;
        profile.CallingCalendarId = string.IsNullOrWhiteSpace(model.CallingCalendarId)
            ? null
            : model.CallingCalendarId.Trim();
        profile.EnforceAbandonmentCap = model.EnforceAbandonmentCap;
        profile.MaxAbandonmentRatePercent = model.MaxAbandonmentRatePercent;
        profile.AbandonmentSampleFloor = model.AbandonmentSampleFloor;
        profile.SafeHarborEnabled = model.SafeHarborEnabled;
        profile.SafeHarborMessage = string.IsNullOrWhiteSpace(model.SafeHarborMessage)
            ? null
            : model.SafeHarborMessage.Trim();
        profile.Enabled = model.Enabled;

        return await EditAsync(profile, context);
    }

    private async Task<DialerAbandonmentStatistics> GetStatisticsAsync(string profileId, TimeSpan window)
    {
        foreach (var provider in _statisticsProviders)
        {
            var statistics = await provider.GetStatisticsAsync(profileId, window);

            if (statistics is not null)
            {
                return statistics;
            }
        }

        return null;
    }
}
