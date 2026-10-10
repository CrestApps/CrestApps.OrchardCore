using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;

/// <summary>
/// Puts the messages that were already waiting for a retry when the outbox index was added into that index, by saving
/// them again. Only recent messages can still be waiting, so only those are read.
/// </summary>
internal static class MessagingOutboxBackfill
{
    private const int PageSize = 200;

    private static readonly TimeSpan _lookBack = TimeSpan.FromDays(2);

    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        var session = serviceProvider.GetRequiredService<ISession>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MessagingOutboxBackfill));
        var queued = MessageDeliveryStatus.Queued.ToString();
        var since = clock.UtcNow.Subtract(_lookBack);
        var skip = 0;
        var resaved = 0;

        try
        {
            while (true)
            {
                var page = await session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                        index => !index.IsInbound && index.CreatedUtc >= since,
                        collection: OmnichannelConstants.CollectionName)
                    .OrderBy(index => index.CreatedUtc)
                    .Skip(skip)
                    .Take(PageSize)
                    .ListAsync();

                var messages = page.ToArray();

                if (messages.Length == 0)
                {
                    break;
                }

                skip += messages.Length;

                foreach (var message in messages)
                {
                    if (string.Equals(message.DeliveryStatus, queued, StringComparison.Ordinal))
                    {
                        await session.SaveAsync(message, collection: OmnichannelConstants.CollectionName);
                        resaved++;
                    }
                }

                await session.SaveChangesAsync();
            }

            if (resaved > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Added {Count} waiting outbound messages to the messaging outbox index.", resaved);
            }
        }
        catch (Exception ex)
        {
            // A message missed here is only a retry that does not happen; the upgrade itself must not fail over it.
            logger.LogError(ex, "Could not add the waiting outbound messages to the messaging outbox index.");
        }
    }
}
