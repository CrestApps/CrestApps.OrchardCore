using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Runs the <c>Convert lead</c> subject action: when a disposition such as <c>Qualified</c> is chosen for a lead's
/// activity, the lead is converted and the actions that follow work on the contact it became.
/// </summary>
internal sealed class ConvertLeadSubjectActionHandler : ISubjectActionHandler
{
    private readonly IUnattendedLeadConverter _converter;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConvertLeadSubjectActionHandler"/> class.
    /// </summary>
    /// <param name="converter">Converts the lead with nobody at the screen to choose the contact.</param>
    /// <param name="logger">The logger.</param>
    public ConvertLeadSubjectActionHandler(
        IUnattendedLeadConverter converter,
        ILogger<ConvertLeadSubjectActionHandler> logger)
    {
        _converter = converter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ActionType => OmnichannelConstants.ActionTypes.ConvertLead;

    /// <inheritdoc/>
    public int Order => -100;

    /// <inheritdoc/>
    public async Task ExecuteAsync(SubjectAction action, SubjectActionExecutionContext context)
    {
        var lead = context.Contact;

        if (lead is null || !lead.TryGet<LeadPart>(out var leadPart) || leadPart.IsConverted)
        {
            return;
        }

        if (!action.TryGet<ConvertLeadActionMetadata>(out var metadata))
        {
            metadata = new ConvertLeadActionMetadata();
        }

        var result = await _converter.ConvertAsync(lead, new LeadConversionRequest
        {
            AccountMode = metadata.AccountMode,
            CreateOpportunity = metadata.CreateOpportunity,
            OpportunityContentType = metadata.OpportunityContentType,
            CampaignId = context.Activity.CampaignId,
            OpenActivities = metadata.OpenActivities,
            CompletingActivityId = context.Activity.ItemId,
            UserId = context.Activity.CompletedById,
            UserName = context.Activity.CompletedByUsername,
        });

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "The Convert lead action of activity {ActivityId} could not convert lead {LeadId}: {Errors}",
                context.Activity.ItemId.SanitizeLogValue(),
                lead.ContentItemId.SanitizeLogValue(),
                string.Join(" ", result.Errors));

            return;
        }

        // The follow-up actions of this disposition work on the contact now.
        context.Contact = result.Contact ?? context.Contact;
    }
}
