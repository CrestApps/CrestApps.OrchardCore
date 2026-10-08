using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows;

/// <summary>
/// Starts the <see cref="LeadConvertedEvent"/> workflows once a lead is converted.
/// </summary>
internal sealed class LeadConvertedWorkflowHandler : ILeadConversionHandler
{
    private readonly IWorkflowManager _workflowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadConvertedWorkflowHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager.</param>
    public LeadConvertedWorkflowHandler(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    /// <inheritdoc/>
    public async Task ConvertedAsync(LeadConversionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Lead is null || context.Contact is null)
        {
            return;
        }

        // The contact is the workflow's content item, so the content tasks that follow act on the customer, not on
        // the closed lead.
        var input = new Dictionary<string, object>
        {
            ["ContentItem"] = context.Contact,
            ["Lead"] = context.Lead,
            ["LeadContentItemId"] = context.Lead.ContentItemId,
            ["ContactContentItemId"] = context.Contact.ContentItemId,
            ["AccountContentItemId"] = context.Account?.ContentItemId,
            ["OpportunityContentItemId"] = context.Opportunity?.ContentItemId,
            ["ContactCreated"] = context.ContactCreated,
        };

        await _workflowManager.TriggerEventAsync(nameof(LeadConvertedEvent), input, correlationId: context.Contact.ContentItemId);
    }
}
