using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Outreach;

/// <summary>
/// Pages a broadcast's recipient list, with the position in the list as the cursor. Recipients the broadcast processed
/// before it was sent through the outreach engine are left out, so a half-sent broadcast never sends to them again.
/// </summary>
public sealed class MessagingBroadcastAudienceSource : IOutreachAudienceSource
{
    private readonly IMessagingBroadcastStore _broadcastStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingBroadcastAudienceSource"/> class.
    /// </summary>
    /// <param name="broadcastStore">The broadcast store.</param>
    public MessagingBroadcastAudienceSource(IMessagingBroadcastStore broadcastStore)
    {
        _broadcastStore = broadcastStore;
    }

    /// <inheritdoc/>
    public string Name
        => MessagingOutreachConstants.BroadcastAudienceSource;

    /// <inheritdoc/>
    public async Task<OutreachAudiencePage> GetPageAsync(OutreachRun run, string cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var broadcast = string.IsNullOrEmpty(run.OwnerId) ? null : await _broadcastStore.FindByIdAsync(run.OwnerId, cancellationToken);

        if (broadcast is null)
        {
            return OutreachAudiencePage.Empty;
        }

        var recipients = broadcast.Recipients ?? [];
        var offset = string.IsNullOrEmpty(cursor) ? 0 : int.Parse(cursor, CultureInfo.InvariantCulture);
        var end = Math.Min(recipients.Count, offset + Math.Max(1, pageSize));
        var processed = new HashSet<string>(broadcast.ProcessedRecipients ?? [], StringComparer.OrdinalIgnoreCase);
        var page = new List<OutreachRecipient>(end - offset);

        for (var index = offset; index < end; index++)
        {
            var address = recipients[index];

            if (string.IsNullOrWhiteSpace(address) || processed.Contains(address))
            {
                continue;
            }

            page.Add(new OutreachRecipient
            {
                Key = address.Trim().ToUpperInvariant(),
                Address = address.Trim(),
            });
        }

        return new OutreachAudiencePage
        {
            Recipients = page,
            NextCursor = end < recipients.Count ? end.ToString(CultureInfo.InvariantCulture) : null,
        };
    }
}
