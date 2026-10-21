using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Notifications;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Builds the real <see cref="DefaultCustomerOwnerNotifier"/> over a test's notification, user and email doubles.
/// </summary>
internal static class TestCustomerOwnerNotifier
{
    /// <summary>
    /// A notifier that reaches users through <paramref name="notifications"/> and guests by the email service
    /// <paramref name="services"/> holds, at the contact <paramref name="contacts"/> resolves (the guest's own snapshot
    /// when it is <see langword="null"/>).
    /// </summary>
    public static ICustomerOwnerNotifier Create(
        INotificationService notifications,
        IUserService users,
        ICustomerContactResolver contacts = null,
        IServiceProvider services = null)
        => new DefaultCustomerOwnerNotifier(
            users,
            contacts ?? new DefaultCustomerContactResolver(users),
            new NotificationServiceProvider(notifications, services),
            NullLogger<DefaultCustomerOwnerNotifier>.Instance);

    private sealed class NotificationServiceProvider : IServiceProvider
    {
        private readonly INotificationService _notifications;
        private readonly IServiceProvider _services;

        public NotificationServiceProvider(INotificationService notifications, IServiceProvider services)
        {
            _notifications = notifications;
            _services = services;
        }

        public object GetService(Type serviceType)
            => serviceType == typeof(INotificationService)
                ? _notifications
                : _services?.GetService(serviceType);
    }
}
