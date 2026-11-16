using System.Net;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Timeline.Notifications;

/// <summary>
/// Tells the users an entry mentions with <c>@username</c> that they were mentioned, through Orchard Core
/// notifications. A user who may not read the entry is not told, so a mention never leaks an internal note.
/// </summary>
internal sealed partial class TimelineMentionHandler : ITimelineEntryHandler
{
    private const int MaxMentions = 20;

    private readonly UserManager<IUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<IUser> _principalFactory;
    private readonly ITimelineAccessService _accessService;
    private readonly IRecordDirectory _recordDirectory;
    private readonly INotificationService _notificationService;

    internal readonly IStringLocalizer S;

    public TimelineMentionHandler(
        UserManager<IUser> userManager,
        IUserClaimsPrincipalFactory<IUser> principalFactory,
        ITimelineAccessService accessService,
        IRecordDirectory recordDirectory,
        INotificationService notificationService,
        IStringLocalizer<TimelineMentionHandler> stringLocalizer)
    {
        _userManager = userManager;
        _principalFactory = principalFactory;
        _accessService = accessService;
        _recordDirectory = recordDirectory;
        _notificationService = notificationService;
        S = stringLocalizer;
    }

    public async Task AddedAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
    {
        var names = FindMentions(entry?.Body);

        if (names.Count == 0)
        {
            return;
        }

        var record = await _recordDirectory.GetSummaryAsync(entry.Subject, cancellationToken);
        var title = record?.Title ?? entry.Subject.ToString();

        foreach (var name in names)
        {
            var user = await _userManager.FindByNameAsync(name);

            if (user is null || string.Equals(await _userManager.GetUserIdAsync(user), entry.Author.Id, StringComparison.Ordinal))
            {
                continue;
            }

            var principal = await _principalFactory.CreateAsync(user);

            if (!await _accessService.CanViewAsync(principal, entry.Subject, entry.Visibility, cancellationToken))
            {
                continue;
            }

            // The entry's text stays on the record: a notification and its e-mail keep what they were sent, so a body
            // redacted later would live on in them. The reader follows the link to read it.
            var encodedTitle = WebUtility.HtmlEncode(title);

            var message = new NotificationMessage
            {
                Subject = S["You were mentioned on {0}", title],
                Summary = S["You were mentioned on {0}.", encodedTitle],
                TextBody = string.IsNullOrEmpty(record?.AdminUrl)
                    ? S["You were mentioned on {0}.", title]
                    : S["You were mentioned on {0}: {1}", title, record.AdminUrl],
                HtmlBody = string.IsNullOrEmpty(record?.AdminUrl)
                    ? S["You were mentioned on {0}.", encodedTitle]
                    : S["You were mentioned on <a href=\"{0}\">{1}</a>.", WebUtility.HtmlEncode(record.AdminUrl), encodedTitle],
                Type = TimelineConstants.MentionNotificationType,
                ReferenceId = entry.Subject.ToString(),
                Url = record?.AdminUrl,
            };
            message.Put(new TimelineMentionInfo { Subject = entry.Subject.ToString(), EntryId = entry.ItemId, Visibility = entry.Visibility });

            await _notificationService.SendAsync(user, message, cancellationToken);
        }
    }

    /// <summary>
    /// Finds the user names a text mentions with <c>@username</c>, each once. An e-mail address is not a mention.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The user names.</returns>
    internal static IReadOnlyList<string> FindMentions(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return [.. MentionPattern().Matches(text)
            .Select(match => match.Groups["name"].Value.TrimEnd('.', '-', '_'))
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxMentions)];
    }

    [GeneratedRegex(@"(?<![\w@.])@(?<name>[A-Za-z0-9][A-Za-z0-9._\-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex MentionPattern();
}
