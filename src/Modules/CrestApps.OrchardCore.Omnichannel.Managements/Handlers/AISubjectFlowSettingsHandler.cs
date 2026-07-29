using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Declares the rules an automated subject flow must satisfy before it can be stored.
/// </summary>
/// <remarks>
/// These rules are registered with the AI feature rather than with the subject flow itself, because a tenant that
/// does not run AI has no automated flows to hold to them.
/// </remarks>
internal sealed class AISubjectFlowSettingsHandler : CatalogEntryHandlerBase<SubjectFlowSettings>
{
    private readonly IAIProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AISubjectFlowSettingsHandler"/> class.
    /// </summary>
    /// <param name="profileManager">The manager used to resolve the AI profile a flow names.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AISubjectFlowSettingsHandler(
        IAIProfileManager profileManager,
        IStringLocalizer<AISubjectFlowSettingsHandler> stringLocalizer)
    {
        _profileManager = profileManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<SubjectFlowSettings> context, CancellationToken cancellationToken = default)
    {
        var flowSettings = context.Model;

        if (flowSettings.InteractionType != ActivityInteractionType.Automated)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(flowSettings.SubjectGoal))
        {
            context.Result.Fail(new ValidationResult(S["Subject goal is required for automated interactions."], [nameof(SubjectFlowSettings.SubjectGoal)]));
        }

        if (string.IsNullOrWhiteSpace(flowSettings.ProfileId))
        {
            context.Result.Fail(new ValidationResult(S["AI profile is required for automated interactions."], [nameof(SubjectFlowSettings.ProfileId)]));
        }
        else
        {
            var profile = await _profileManager.FindByIdAsync(flowSettings.ProfileId, cancellationToken);

            if (profile is null || profile.Type != AIProfileType.Chat)
            {
                context.Result.Fail(new ValidationResult(S["The selected AI profile is invalid."], [nameof(SubjectFlowSettings.ProfileId)]));
            }
            else if (string.IsNullOrWhiteSpace(profile.GetOrCreate<AIProfileMetadata>().InitialPrompt))
            {
                context.Result.Fail(new ValidationResult(S["The selected AI profile must have Add initial prompt enabled."], [nameof(SubjectFlowSettings.ProfileId)]));
            }
        }

        if (flowSettings.NoResponseTimeoutInMinutes is <= 0)
        {
            context.Result.Fail(new ValidationResult(S["No-response timeout must be greater than zero minutes."], [nameof(SubjectFlowSettings.NoResponseTimeoutInMinutes)]));
        }

        if (flowSettings.SmsResponseDelayInSeconds is < 0)
        {
            context.Result.Fail(new ValidationResult(S["SMS response delay cannot be negative."], [nameof(SubjectFlowSettings.SmsResponseDelayInSeconds)]));
        }
    }
}
