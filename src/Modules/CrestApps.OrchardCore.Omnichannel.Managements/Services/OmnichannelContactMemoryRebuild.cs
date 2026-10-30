using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Rebuilds a contact's AI memory note from the AI conversations of their activities, oldest first.
/// </summary>
internal static class OmnichannelContactMemoryRebuild
{
    /// <summary>
    /// Rebuilds the note.
    /// </summary>
    /// <param name="services">The scope's services.</param>
    /// <param name="contactId">The contact.</param>
    /// <returns>A task that completes when the note is rebuilt.</returns>
    public static async Task RunAsync(IServiceProvider services, string contactId)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(OmnichannelContactMemoryRebuild));

        try
        {
            var session = services.GetRequiredService<ISession>();
            var activities = await session.Query<OmnichannelActivity, OmnichannelActivityIndex>(
                    index => index.ContactContentItemId == contactId,
                    collection: OmnichannelConstants.CollectionName)
                .ListAsync();

            var sessionIds = activities
                .Where(activity => !string.IsNullOrEmpty(activity.AISessionId))
                .OrderBy(activity => activity.CreatedUtc)
                .Select(activity => activity.AISessionId)
                .ToList();

            var note = await services.GetRequiredService<AIContactMemoryService>().RebuildAsync(contactId, sessionIds);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "The AI memory of contact '{ContactId}' was rebuilt from {Count} conversation(s){Outcome}.",
                    contactId.SanitizeLogValue(),
                    sessionIds.Count,
                    note is null ? " with nothing to remember" : string.Empty);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The AI memory of contact '{ContactId}' could not be rebuilt.", contactId.SanitizeLogValue());
        }
    }
}
