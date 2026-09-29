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
    private readonly ILeadConversionService _conversionService;
    private readonly LeadMatchFinder _matchFinder;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConvertLeadSubjectActionHandler"/> class.
    /// </summary>
    /// <param name="conversionService">The lead conversion service.</param>
    /// <param name="matchFinder">Finds the contact a lead may already be.</param>
    /// <param name="logger">The logger.</param>
    public ConvertLeadSubjectActionHandler(
        ILeadConversionService conversionService,
        LeadMatchFinder matchFinder,
        ILogger<ConvertLeadSubjectActionHandler> logger)
    {
        _conversionService = conversionService;
        _matchFinder = matchFinder;
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

        // An unattended conversion merges into an existing contact only when exactly one shares the lead's number or
        // email; with more than one it cannot tell which, so it creates a new contact rather than guess.
        var matches = await _matchFinder.FindContactsAsync(lead);

        var result = await _conversionService.ConvertAsync(new LeadConversionRequest
        {
            LeadContentItemId = lead.ContentItemId,
            ExistingContactItemId = matches.Count == 1 ? matches[0].ContentItemId : null,
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
