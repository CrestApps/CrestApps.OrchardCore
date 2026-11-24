using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers observers of account invitations.
/// </summary>
public static class AccountInvitationServiceCollectionExtensions
{
    /// <summary>
    /// Registers an observer of account invitations.
    /// </summary>
    public static IServiceCollection AddAccountInvitationObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IAccountInvitationObserver
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAccountInvitationObserver, TObserver>());

        return services;
    }
}
