using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Entities;
using OrchardCore.Localization;
using OrchardCore.Navigation.Core;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Notifications.Services;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Tests.Agent.Notifications;

/// <summary>
/// Stands in for the Notifications services in a tool test: notifications kept in memory behind the notification
/// manager, the filter parser Orchard Core builds for the Notifications admin page, and a query service that records
/// the filter it was given. One notification belongs to another user and holds <see cref="Canary"/>, so a test can
/// tell that no tool returns or changes it.
/// </summary>
internal sealed class NotificationToolDoubles
{
    /// <summary>
    /// The text the notification of the other user holds.
    /// </summary>
    public const string Canary = "CANARY";

    /// <summary>
    /// The ID of the notification of the other user.
    /// </summary>
    public const string OtherUserNotificationId = "note-other";

    public NotificationToolDoubles()
    {
        Add("note-1", AgentToolTestContext.UserId, "Import failed", "The product import failed.", "Import", read: false);
        Add("note-2", AgentToolTestContext.UserId, "You were mentioned", "Jane mentioned you in a comment.", "Mention", read: false);
        Add("note-3", AgentToolTestContext.UserId, "Report is ready", "The sales report is ready.", "Report", read: true);
        Add(OtherUserNotificationId, "user-jane", $"{Canary} salary review", $"{Canary} a private note for Jane.", "Mention", read: false);
    }

    /// <summary>
    /// Gets the notifications the manager knows, of every user.
    /// </summary>
    public List<Notification> Notifications { get; } = [];

    /// <summary>
    /// Gets the filter of the last query of the list, as the parser of the admin page understood it.
    /// </summary>
    public string NormalizedFilter { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the query service returns the notifications of every user. The one of
    /// Orchard Core never does; a tool must not rely on that alone.
    /// </summary>
    public bool QueryReturnsEveryUser { get; set; }

    /// <summary>
    /// Whether a notification is marked as read.
    /// </summary>
    /// <param name="notificationId">The ID of the notification.</param>
    /// <returns><see langword="true"/> when the notification is read.</returns>
    public bool IsRead(string notificationId)
        => IsRead(Notifications.First(notification => notification.NotificationId == notificationId));

    /// <summary>
    /// Adds the doubles to the services of a tool call.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    public void AddTo(IServiceCollection services)
    {
        var builder = new QueryEngineBuilder<Notification>();
        new DefaultNotificationsAdminListFilterProvider().Build(builder);

        var types = new NotificationTypeOptions();
        types.Add("Mention", LocalizationSource.Create("Mention"));
        types.Add("Import", LocalizationSource.Create("Import"));

        services.AddSingleton<INotificationAdminListFilterParser>(new DefaultNotificationAdminListFilterParser(builder.Build()));
        services.AddSingleton<INotificationManager>(new InMemoryNotificationManager(this));
        services.AddSingleton<INotificationsAdminListQueryService>(new QueryService(this));
        services.AddSingleton(Options.Create(types));
    }

    private static bool IsRead(Notification notification)
        => notification.TryGet<NotificationReadInfo>(out var readInfo) && readInfo.IsRead;

    private void Add(string id, string userId, string subject, string summary, string type, bool read)
    {
        var notification = new Notification
        {
            NotificationId = id,
            UserId = userId,
            Subject = subject,
            Summary = summary,
            Type = type,
            CreatedUtc = new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc).AddMinutes(-Notifications.Count),
        };

        notification.Put(new NotificationReadInfo
        {
            IsRead = read,
        });

        Notifications.Add(notification);
    }

    private sealed class InMemoryNotificationManager : INotificationManager
    {
        private readonly NotificationToolDoubles _doubles;

        public InMemoryNotificationManager(NotificationToolDoubles doubles)
        {
            _doubles = doubles;
        }

        public Task<Notification> FindByIdAsync(string notificationId, CancellationToken cancellationToken = default)
            => Task.FromResult(_doubles.Notifications.FirstOrDefault(notification => notification.NotificationId == notificationId));

        public Task<IReadOnlyList<Notification>> ListAsync(NotificationFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Notification>>(Match(filter).ToList());

        public Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            _doubles.Notifications.Remove(notification);

            return Task.CompletedTask;
        }

        public Task<int> DeleteAsync(NotificationFilter filter, CancellationToken cancellationToken = default)
        {
            var matches = Match(filter).ToList();

            _doubles.Notifications.RemoveAll(matches.Contains);

            return Task.FromResult(matches.Count);
        }

        public Task<bool> MarkAsReadAsync(Notification notification, CancellationToken cancellationToken = default)
            => Task.FromResult(Mark(notification, true));

        public Task<bool> MarkAsUnreadAsync(Notification notification, CancellationToken cancellationToken = default)
            => Task.FromResult(Mark(notification, false));

        public Task<int> MarkAsReadAsync(NotificationFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult(Match(filter).Count(notification => Mark(notification, true)));

        public bool IsRead(Notification notification)
            => NotificationToolDoubles.IsRead(notification);

        private IEnumerable<Notification> Match(NotificationFilter filter)
        {
            if (!filter.HasCriteria)
            {
                throw new InvalidOperationException("The filter sets no criteria.");
            }

            return _doubles.Notifications
                .Where(notification => string.IsNullOrEmpty(filter.UserId) || notification.UserId == filter.UserId)
                .Where(notification => string.IsNullOrEmpty(filter.Type) || notification.Type == filter.Type)
                .Where(notification => filter.IsRead is null || NotificationToolDoubles.IsRead(notification) == filter.IsRead);
        }

        private static bool Mark(Notification notification, bool read)
        {
            var readInfo = notification.GetOrCreate<NotificationReadInfo>();

            if (readInfo.IsRead == read)
            {
                return false;
            }

            readInfo.IsRead = read;
            notification.Put(readInfo);

            return true;
        }
    }

    private sealed class QueryService : INotificationsAdminListQueryService
    {
        private readonly NotificationToolDoubles _doubles;

        public QueryService(NotificationToolDoubles doubles)
        {
            _doubles = doubles;
        }

        public Task<NotificationQueryResult> QueryAsync(int page, int pageSize, ListNotificationOptions options, IUpdateModel updater)
        {
            _doubles.NormalizedFilter = options.FilterResult.ToNormalizedString();

            // The query of Orchard Core always keeps to the user of the request.
            var notifications = _doubles.Notifications
                .Where(notification => _doubles.QueryReturnsEveryUser || notification.UserId == AgentToolTestContext.UserId)
                .ToList();

            return Task.FromResult(new NotificationQueryResult
            {
                Notifications = notifications.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                TotalCount = notifications.Count,
            });
        }
    }
}
