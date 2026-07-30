using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class DialerProfileHandler : CatalogEntryHandlerBase<DialerProfile>
{
    private readonly IClock _clock;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock used to stamp audit times.</param>
    /// <param name="shellFeaturesManager">The shell features manager used to detect the Automated Dialer feature.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerProfileHandler(
        IClock clock,
        IShellFeaturesManager shellFeaturesManager,
        IStringLocalizer<DialerProfileHandler> stringLocalizer)
    {
        _clock = clock;
        _shellFeaturesManager = shellFeaturesManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<DialerProfile> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<DialerProfile> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<DialerProfile> context, CancellationToken cancellationToken = default)
    {
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<DialerProfile> context, CancellationToken cancellationToken = default)
    {
        var profile = context.Model;

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(DialerProfile.Name)]));
        }

        if (profile.Mode == DialerMode.Predictive)
        {
            context.Result.Fail(new ValidationResult(S["Predictive dialing is not available yet. Choose Manual, Preview, Power, or Progressive."], [nameof(DialerProfile.Mode)]));
        }
        else if (profile.Mode.RequiresAutomatedDialerFeature() &&
            !await _shellFeaturesManager.IsFeatureEnabledAsync(ContactCenterConstants.Feature.DialerAutomated))
        {
            context.Result.Fail(new ValidationResult(S["Enable the Contact Center Automated Dialer feature before using Power or Progressive dialing."], [nameof(DialerProfile.Mode)]));
        }

        if (profile.CallsPerAgent < 1 || profile.CallsPerAgent > PowerDialerStrategy.MaxCallsPerAgent)
        {
            context.Result.Fail(new ValidationResult(S["The calls per agent must be between 1 and {0}.", PowerDialerStrategy.MaxCallsPerAgent], [nameof(DialerProfile.CallsPerAgent)]));
        }

        if (profile.EnforceCallingWindow && string.IsNullOrWhiteSpace(profile.CallingCalendarId))
        {
            context.Result.Fail(new ValidationResult(S["Select an outbound calling calendar when calling-window enforcement is enabled."], [nameof(DialerProfile.CallingCalendarId)]));
        }

        if (profile.MaxAbandonmentRatePercent is < 0 or > 100)
        {
            context.Result.Fail(new ValidationResult(S["The maximum abandonment rate must be between 0 and 100 percent."], [nameof(DialerProfile.MaxAbandonmentRatePercent)]));
        }

        if (profile.AbandonmentSampleFloor < 0)
        {
            context.Result.Fail(new ValidationResult(S["The abandonment sample floor cannot be negative."], [nameof(DialerProfile.AbandonmentSampleFloor)]));
        }

        if (profile.EnforceAbandonmentCap && profile.Mode.IsAutomated() && !profile.SafeHarborEnabled)
        {
            context.Result.Fail(new ValidationResult(S["Enable safe-harbor messaging when an automated dialing mode enforces an abandonment cap."], [nameof(DialerProfile.SafeHarborEnabled)]));
        }

        if (profile.SafeHarborEnabled && string.IsNullOrWhiteSpace(profile.SafeHarborMessage))
        {
            context.Result.Fail(new ValidationResult(S["Provide a safe-harbor announcement when safe-harbor messaging is enabled."], [nameof(DialerProfile.SafeHarborMessage)]));
        }
    }
}
