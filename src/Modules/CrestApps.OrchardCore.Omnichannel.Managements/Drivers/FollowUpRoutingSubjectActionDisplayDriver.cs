using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Edits who makes the call a "Try again" or "New activity" subject action creates: an agent, the dialer under a
/// dialer profile, or the same as before, and on which campaign.
/// </summary>
internal sealed class FollowUpRoutingSubjectActionDisplayDriver : DisplayDriver<SubjectAction>
{
    private readonly BulkActivityAdminFormOptionsProvider _formOptions;

    internal readonly IStringLocalizer S;

    public FollowUpRoutingSubjectActionDisplayDriver(
        BulkActivityAdminFormOptionsProvider formOptions,
        IStringLocalizer<FollowUpRoutingSubjectActionDisplayDriver> stringLocalizer)
    {
        _formOptions = formOptions;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SubjectAction action, BuildEditorContext context)
    {
        if (!CreatesFollowUp(action))
        {
            return null;
        }

        return Initialize<FollowUpRoutingSubjectActionViewModel>("FollowUpRoutingSubjectAction_Edit", async model =>
        {
            if (!action.TryGet<FollowUpRoutingMetadata>(out var routing))
            {
                routing = new FollowUpRoutingMetadata();
            }

            model.Target = routing.Target;
            model.CampaignId = routing.CampaignId;
            model.DialerProfileId = routing.DialerProfileId;
            model.Source = routing.Source;
            model.Campaigns = await _formOptions.GetCampaignOptionsAsync(routing.CampaignId, S["Keep its campaign"]);
            model.DialerProfiles = await _formOptions.GetDialerProfileOptionsAsync(routing.DialerProfileId, S["Choose a dialer profile"]);

            var selectedSource = string.IsNullOrWhiteSpace(routing.Source) ? ActivitySources.Manual : routing.Source;
            model.Sources = _formOptions.GetManuallyAssignableSourceOptions(S["Manual"])
                .Where(source => !string.IsNullOrEmpty(source.Value) && ActivityRerouter.IsAgentSource(source.Value))
                .ToList();

            foreach (var source in model.Sources)
            {
                source.Selected = string.Equals(source.Value, selectedSource, StringComparison.OrdinalIgnoreCase);
            }
        }).Location("Content:6");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubjectAction action, UpdateEditorContext context)
    {
        if (!CreatesFollowUp(action))
        {
            return null;
        }

        var model = new FollowUpRoutingSubjectActionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Target == FollowUpRoutingTarget.Dialer && string.IsNullOrWhiteSpace(model.DialerProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DialerProfileId), S["Choose the dialer profile that calls the follow-up."]);
        }

        if (model.Target == FollowUpRoutingTarget.Agent &&
            !string.IsNullOrWhiteSpace(model.Source) &&
            !ActivityRerouter.IsAgentSource(model.Source))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Source), S["Choose a source an agent's call may have."]);
        }

        action.Put(new FollowUpRoutingMetadata
        {
            Target = model.Target,
            CampaignId = Normalize(model.CampaignId),
            DialerProfileId = model.Target == FollowUpRoutingTarget.Dialer ? Normalize(model.DialerProfileId) : null,
            Source = model.Target == FollowUpRoutingTarget.Agent ? Normalize(model.Source) : null,
        });

        return Edit(action, context);
    }

    private static bool CreatesFollowUp(SubjectAction action)
        => string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
