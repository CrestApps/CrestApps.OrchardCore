using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// The Outreach audience of a mailing list (<see cref="MailingListsConstants.OutreachAudienceSource"/>): its subscribed
/// members, a page at a time in the order they were stored. The run names the list in its
/// <see cref="MailingListsConstants.OutreachListIdProperty"/> property, or by its owner when the run is owned by a list.
/// </summary>
public sealed class MailingListAudienceSource : IOutreachAudienceSource
{
    private readonly IMailingListMemberStore _members;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListAudienceSource"/> class.
    /// </summary>
    /// <param name="members">The member store.</param>
    public MailingListAudienceSource(IMailingListMemberStore members)
    {
        _members = members;
    }

    /// <inheritdoc/>
    public string Name
        => MailingListsConstants.OutreachAudienceSource;

    /// <inheritdoc/>
    public async Task<OutreachAudiencePage> GetPageAsync(OutreachRun run, string cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var listId = ListIdOf(run);

        if (string.IsNullOrEmpty(listId))
        {
            return OutreachAudiencePage.Empty;
        }

        var (members, next) = await _members.ListSubscribedAsync(listId, cursor, Math.Max(1, pageSize), cancellationToken);

        return new OutreachAudiencePage
        {
            Recipients = [.. members.Select(member => new OutreachRecipient
            {
                Key = member.ItemId,
                Address = string.IsNullOrWhiteSpace(member.Contact?.Email) ? member.NormalizedEmail : member.Contact.Email,
                DisplayName = member.Contact?.Name,
            })],
            NextCursor = next,
        };
    }

    /// <summary>
    /// Returns the id of the list an Outreach run sends to.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <returns>The list's id, or <see langword="null"/>.</returns>
    public static string ListIdOf(OutreachRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.Properties is not null
            && run.Properties.TryGetValue(MailingListsConstants.OutreachListIdProperty, out var value)
            && Text(value) is { Length: > 0 } listId)
        {
            return listId;
        }

        return string.Equals(run.OwnerType, MailingListsConstants.OutreachAudienceSource, StringComparison.Ordinal) ? run.OwnerId : null;
    }

    private static string Text(object value)
        => value switch
        {
            string text => text.Trim(),
            JsonValue node when node.TryGetValue<string>(out var text) => text?.Trim(),
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()?.Trim(),
            _ => null,
        };
}
