using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Drivers;

/// <summary>
/// Display driver for the <see cref="RerouteActivityTask"/> workflow activity.
/// </summary>
public sealed class RerouteActivityTaskDisplayDriver : ActivityDisplayDriver<RerouteActivityTask, RerouteActivityTaskViewModel>
{
    private readonly BulkActivityAdminFormOptionsProvider _formOptions;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RerouteActivityTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="formOptions">The provider of the campaign, dialer profile and source choices.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager used to validate the expressions.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RerouteActivityTaskDisplayDriver(
        BulkActivityAdminFormOptionsProvider formOptions,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<RerouteActivityTaskDisplayDriver> stringLocalizer)
    {
        _formOptions = formOptions;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(RerouteActivityTask activity, RerouteActivityTaskViewModel model)
    {
        model.ActivityItemId = activity.ActivityItemId;
        model.Target = activity.Target;
        model.CampaignId = activity.CampaignId;
        model.DialerProfileId = activity.DialerProfileId;
        model.Source = activity.Source;
        model.UserName = activity.UserName;
        model.CreateNextAttempt = activity.CreateNextAttempt;
        model.DelayMinutes = activity.DelayMinutes;
        model.Campaigns = await _formOptions.GetCampaignOptionsAsync(activity.CampaignId, S["Keep its campaign"]);
        model.DialerProfiles = await _formOptions.GetDialerProfileOptionsAsync(activity.DialerProfileId, S["Choose a dialer profile"]);
        // An agent makes the call, so neither the AI's source nor a dialer's is offered.
        var selectedSource = activity.Source ?? ActivitySources.Manual;
        model.Sources = _formOptions.GetManuallyAssignableSourceOptions(S["Manual"])
            .Where(source => !string.IsNullOrEmpty(source.Value) && ActivityRerouter.IsAgentSource(source.Value))
            .ToList();

        foreach (var source in model.Sources)
        {
            source.Selected = string.Equals(source.Value, selectedSource, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(RerouteActivityTask activity, UpdateEditorContext context)
    {
        var model = new RerouteActivityTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ActivityItemId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityItemId), S["The activity is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.ActivityItemId, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ActivityItemId), S["The activity expression is invalid."]);
        }

        if (model.Target == ActivityRerouteTarget.Agent &&
            !string.IsNullOrWhiteSpace(model.UserName) &&
            !_liquidTemplateManager.Validate(model.UserName, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserName), S["The agent expression is invalid."]);
        }

        if (!string.IsNullOrWhiteSpace(model.DelayMinutes) && !_liquidTemplateManager.Validate(model.DelayMinutes, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DelayMinutes), S["The delay expression is invalid."]);
        }

        if (model.Target == ActivityRerouteTarget.Dialer && string.IsNullOrWhiteSpace(model.DialerProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DialerProfileId), S["Choose the dialer profile that calls the activity."]);
        }

        if (model.Target == ActivityRerouteTarget.KeepRoute && string.IsNullOrWhiteSpace(model.CampaignId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CampaignId), S["Choose the campaign to move the activity to."]);
        }

        activity.ActivityItemId = model.ActivityItemId?.Trim();
        activity.Target = model.Target;
        activity.CampaignId = Normalize(model.CampaignId);
        activity.DialerProfileId = model.Target == ActivityRerouteTarget.Dialer ? Normalize(model.DialerProfileId) : null;
        activity.Source = model.Target == ActivityRerouteTarget.Agent ? Normalize(model.Source) : null;
        activity.UserName = model.Target == ActivityRerouteTarget.Agent ? Normalize(model.UserName) : null;
        activity.CreateNextAttempt = model.CreateNextAttempt;
        activity.DelayMinutes = Normalize(model.DelayMinutes);

        return Edit(activity, context);
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
