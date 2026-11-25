using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Telnyx.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Telnyx.Drivers;

/// <summary>
/// Adds the AI agent picker to a call entry point's routing, for an entry point that hands its calls to an AI voice
/// agent.
/// </summary>
internal sealed class TelnyxEntryPointAIAgentDisplayDriver : DisplayDriver<ContactCenterEntryPoint>
{
    private readonly IAIProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxEntryPointAIAgentDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profiles.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TelnyxEntryPointAIAgentDisplayDriver(
        IAIProfileManager profileManager,
        IStringLocalizer<TelnyxEntryPointAIAgentDisplayDriver> stringLocalizer)
    {
        _profileManager = profileManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ContactCenterEntryPoint entryPoint, BuildEditorContext context)
    {
        if (!AnswersCalls(entryPoint))
        {
            return null;
        }

        return Initialize<EntryPointAIAgentViewModel>("EntryPointAIAgent_Edit", async model =>
        {
            model.TargetAIProfileId = entryPoint.TargetAIProfileId;

            var profiles = await _profileManager.GetAsync(AIProfileType.Chat);

            model.ProfileOptions = profiles
                .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(profile => new SelectListItem(
                    profile.DisplayText ?? profile.Name,
                    profile.ItemId,
                    string.Equals(profile.ItemId, entryPoint.TargetAIProfileId, StringComparison.Ordinal)))
                .ToList();
        }).Location(l => l.Zone("Content", "2.1").Card(S["Routing"], "2"));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContactCenterEntryPoint entryPoint, UpdateEditorContext context)
    {
        if (!AnswersCalls(entryPoint))
        {
            return null;
        }

        var model = new EntryPointAIAgentViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Kept only for an AI voice agent target; the entry point's own routing clears it for any other target, and
        // the entry point handler requires it when the target is an AI voice agent.
        entryPoint.TargetAIProfileId = model.TargetType == EntryPointTargetType.AIAgent && !string.IsNullOrWhiteSpace(model.TargetAIProfileId)
            ? model.TargetAIProfileId.Trim()
            : null;

        return Edit(entryPoint, context);
    }

    private static bool AnswersCalls(ContactCenterEntryPoint entryPoint)
        => string.Equals(entryPoint.GetChannel(), OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase);
}
