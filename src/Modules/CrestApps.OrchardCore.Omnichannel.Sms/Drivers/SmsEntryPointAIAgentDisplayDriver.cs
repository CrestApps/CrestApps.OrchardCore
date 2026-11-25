using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Drivers;

/// <summary>
/// Adds the AI agent picker to a text entry point, shown while the entry point routes to an AI agent.
/// </summary>
internal sealed class SmsEntryPointAIAgentDisplayDriver : DisplayDriver<ContactCenterEntryPoint>
{
    private readonly IAIProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsEntryPointAIAgentDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SmsEntryPointAIAgentDisplayDriver(
        IAIProfileManager profileManager,
        IStringLocalizer<SmsEntryPointAIAgentDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
        _profileManager = profileManager;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ContactCenterEntryPoint entryPoint, BuildEditorContext context)
    {
        if (!AnswersTexts(entryPoint))
        {
            return null;
        }

        return Initialize<SmsEntryPointAIAgentViewModel>("SmsEntryPointAIAgent_Edit", async model =>
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
        if (!AnswersTexts(entryPoint))
        {
            return null;
        }

        var model = new SmsEntryPointAIAgentViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Kept only for an AI agent target; the entry point handler requires it when the target is an AI agent.
        entryPoint.TargetAIProfileId = model.TargetType == EntryPointTargetType.AIAgent && !string.IsNullOrWhiteSpace(model.TargetAIProfileId)
            ? model.TargetAIProfileId.Trim()
            : null;

        return Edit(entryPoint, context);
    }

    private static bool AnswersTexts(ContactCenterEntryPoint entryPoint)
        => string.Equals(entryPoint.GetChannel(), OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase);
}
