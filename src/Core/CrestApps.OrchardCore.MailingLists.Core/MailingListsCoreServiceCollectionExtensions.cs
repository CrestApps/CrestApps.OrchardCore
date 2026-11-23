using CrestApps.Core.Services;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.MailingLists;
using CrestApps.OrchardCore.MailingLists.Core.Coupons;
using CrestApps.OrchardCore.MailingLists.Core.Emails;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Core.Sync;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the mailing lists services.
/// </summary>
public static class MailingListsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds the list and member stores, the list manager, the member service, the Local provider and the resolver, the
    /// provider outbox and reconciliation, the e-mails, the consent source, the unsubscribe scope, the retention of
    /// unconfirmed sign-ups and the coupon key. The feature that imports lists registers their rules
    /// (<see cref="MailingListHandler"/>), so a recipe and the editor check the same set.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddMailingListsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSignedLinks();
        services.AddTemplatedEmailOutbox();

        services.TryAddScoped<MailingListStore>();
        services.TryAddScoped<ICatalog<MailingList>>(provider => provider.GetRequiredService<MailingListStore>());
        services.TryAddScoped<MailingListManager>();
        services.TryAddScoped<IMailingListManager>(provider => provider.GetRequiredService<MailingListManager>());
        services.TryAddScoped<ICatalogManager<MailingList>>(provider => provider.GetRequiredService<MailingListManager>());

        services.TryAddScoped<MailingListMemberStore>();
        services.TryAddScoped<IMailingListMemberStore>(provider => provider.GetRequiredService<MailingListMemberStore>());
        services.TryAddScoped<MailingListConfirmationThrottle>();
        services.TryAddScoped<MailingListMemberObserverRunner>();
        services.TryAddScoped<IMailingListEmails, MailingListEmails>();
        services.TryAddScoped<IMailingListMemberService, DefaultMailingListMemberService>();
        services.TryAddScoped<MailingListCouponKeyStore>();

        services.AddMailingListProvider<LocalMailingListProvider>();
        services.TryAddScoped<IMailingListProviderResolver, DefaultMailingListProviderResolver>();

        services.AddLeasedWorkQueue<MailingListSyncItem>(options =>
        {
            options.CollectionName = MailingListsConstants.CollectionName;
            options.RemoveCompletedItems = true;

            // The processor gives a change up after MailingListsOptions.SyncMaxAttempts; this only stops a runaway item.
            options.MaxAttempts = 1_000;
        });
        services.TryAddScoped<IMailingListSyncQueue, MailingListSyncQueue>();
        services.TryAddScoped<MailingListSyncProcessor>();
        services.TryAddScoped<MailingListReconciliation>();

        services.AddRetentionPolicy<PendingMembersRetentionPolicy>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IConsentSource, MailingListConsentSource>());
        services.AddUnsubscribeScope<MailingListUnsubscribeScopeHandler>();

        return services;
    }
}
