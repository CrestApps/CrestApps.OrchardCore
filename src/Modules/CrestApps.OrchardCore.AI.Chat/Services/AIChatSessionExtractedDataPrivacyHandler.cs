using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Privacy;
using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Redacts or clears the Chat Analytics extracted data record of a conversation with the conversation, and deletes
/// it with it.
/// </summary>
public sealed class AIChatSessionExtractedDataPrivacyHandler : AIConversationPrivacyHandlerBase
{
    private const string RecordKey = "AIChatSessionExtractedDataRecord";

    private readonly IAIChatSessionExtractedDataStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionExtractedDataPrivacyHandler"/> class.
    /// </summary>
    /// <param name="store">The extracted data store.</param>
    public AIChatSessionExtractedDataPrivacyHandler(IAIChatSessionExtractedDataStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public override async Task CollectingAsync(AIConversationPrivacyContext context, CancellationToken cancellationToken)
    {
        var session = context.Session;

        // Records are read by profile and the session's start, so the window around the start finds this session's.
        var records = await _store.GetAsync(session.ProfileId, session.CreatedUtc.AddDays(-1), session.CreatedUtc.AddDays(1), cancellationToken);
        var record = records.FirstOrDefault(record => string.Equals(record.SessionId, session.SessionId, StringComparison.Ordinal));

        if (record is null)
        {
            return;
        }

        context.Items[RecordKey] = record;

        foreach (var value in record.Values.Values.SelectMany(values => values))
        {
            context.Texts.Add(value);
        }
    }

    /// <inheritdoc/>
    public override async Task ApplyingAsync(AIConversationPrivacyContext context, CancellationToken cancellationToken)
    {
        if (!context.Items.TryGetValue(RecordKey, out var value) || value is not AIChatSessionExtractedDataRecord record)
        {
            return;
        }

        foreach (var key in record.Values.Keys.ToList())
        {
            record.Values[key] = context.Mode == AIDataStorageMode.MetadataOnly
                ? []
                : record.Values[key].Select(context.Redact).ToList();
        }

        await _store.SaveAsync(record, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task DeletingAsync(AIChatSession session, CancellationToken cancellationToken)
        => _store.DeleteAsync(session.SessionId, cancellationToken);
}
