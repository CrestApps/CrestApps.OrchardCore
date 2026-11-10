using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Brings the opportunity's primary contact field up to date when a content type gains or loses a CRM part. The work
/// is deferred to the end of the request because the definition events are raised while the definition is still being saved.
/// </summary>
internal sealed class CrmContentDefinitionEventHandler : IContentDefinitionEventHandler
{
    private bool _scheduled;

    public void ContentPartAttached(ContentPartAttachedContext context)
        => ScheduleWhenRelevant(context.ContentPartName);

    public void ContentPartDetached(ContentPartDetachedContext context)
        => ScheduleWhenRelevant(context.ContentPartName);

    public void ContentTypeImported(ContentTypeImportedContext context)
        => Schedule();

    public void ContentTypeCreated(ContentTypeCreatedContext context)
    {
    }

    public void ContentTypeUpdated(ContentTypeUpdatedContext context)
    {
    }

    public void ContentTypeRemoved(ContentTypeRemovedContext context)
    {
    }

    public void ContentTypeImporting(ContentTypeImportingContext context)
    {
    }

    public void ContentPartCreated(ContentPartCreatedContext context)
    {
    }

    public void ContentPartUpdated(ContentPartUpdatedContext context)
    {
    }

    public void ContentPartRemoved(ContentPartRemovedContext context)
    {
    }

    public void ContentPartImporting(ContentPartImportingContext context)
    {
    }

    public void ContentPartImported(ContentPartImportedContext context)
    {
    }

    public void ContentTypePartUpdated(ContentTypePartUpdatedContext context)
    {
    }

    public void ContentFieldAttached(ContentFieldAttachedContext context)
    {
    }

    public void ContentFieldUpdated(ContentFieldUpdatedContext context)
    {
    }

    public void ContentFieldDetached(ContentFieldDetachedContext context)
    {
    }

    public void ContentPartFieldUpdated(ContentPartFieldUpdatedContext context)
    {
    }

    private void ScheduleWhenRelevant(string partName)
    {
        if (partName is OmnichannelConstants.ContentParts.OmnichannelContact
            or OmnichannelConstants.ContentParts.Lead
            or OmnichannelConstants.ContentParts.Opportunity)
        {
            Schedule();
        }
    }

    private void Schedule()
    {
        if (_scheduled || ShellScope.Current is null)
        {
            return;
        }

        _scheduled = true;

        DeferredWork.Add("omnichannel_opportunity_contact_sync", scope => scope.ServiceProvider
            .GetRequiredService<OpportunityContactFieldSynchronizer>()
            .SynchronizeAsync());
    }
}
