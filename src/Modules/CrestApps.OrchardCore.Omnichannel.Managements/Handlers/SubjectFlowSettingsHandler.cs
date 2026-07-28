using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Declares the rules a subject flow must satisfy before it can be stored, whoever is writing it.
/// </summary>
internal sealed class SubjectFlowSettingsHandler : CatalogEntryHandlerBase<SubjectFlowSettings>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectFlowSettingsHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubjectFlowSettingsHandler(IStringLocalizer<SubjectFlowSettingsHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<SubjectFlowSettings> context, CancellationToken cancellationToken = default)
    {
        var flowSettings = context.Model;

        if (string.IsNullOrWhiteSpace(flowSettings.SubjectContentType))
        {
            context.Result.Fail(new ValidationResult(S["Subject is required."], [nameof(SubjectFlowSettings.SubjectContentType)]));
        }

        if (string.IsNullOrWhiteSpace(flowSettings.CampaignId))
        {
            context.Result.Fail(new ValidationResult(S["Campaign is required."], [nameof(SubjectFlowSettings.CampaignId)]));
        }

        if (string.IsNullOrWhiteSpace(flowSettings.Channel))
        {
            context.Result.Fail(new ValidationResult(S["Channel is required."], [nameof(SubjectFlowSettings.Channel)]));
        }

        if (flowSettings.InteractionType == ActivityInteractionType.Automated &&
            string.IsNullOrWhiteSpace(flowSettings.ChannelEndpointId))
        {
            context.Result.Fail(new ValidationResult(S["Channel endpoint is required for automated interactions."], [nameof(SubjectFlowSettings.ChannelEndpointId)]));
        }

        return Task.CompletedTask;
    }
}
