using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Accounts.Handlers;

/// <summary>
/// Brings the account lists up to date when a content type gains or loses a part. Which parts make a type belong to an
/// account is up to the registered rules, so every attach and detach is looked at; the synchronizer saves nothing when
/// no list changes. The work is deferred to the end of the request because the definition events are raised while the
/// definition is still being saved.
/// </summary>
internal sealed class AccountsContentDefinitionEventHandler : IContentDefinitionEventHandler
{
    private bool _scheduled;

    public void ContentPartAttached(ContentPartAttachedContext context)
        => Schedule();

    public void ContentPartDetached(ContentPartDetachedContext context)
        => Schedule();

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

    private void Schedule()
    {
        if (_scheduled || ShellScope.Current is null)
        {
            return;
        }

        _scheduled = true;

        DeferredWork.Add("account_list_sync", scope => scope.ServiceProvider
            .GetRequiredService<AccountListSynchronizer>()
            .SynchronizeAsync());
    }
}
