using CrestApps.Core.AI.Models;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Indexes.AIChat;

public sealed class AIChatSessionExtractedDataIndex : MapIndex
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public DateTime SessionStartedUtc { get; set; }

    public DateTime? SessionEndedUtc { get; set; }

    public int FieldCount { get; set; }

    public string FieldNames { get; set; }

    public string ValuesText { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

public sealed class AIChatSessionExtractedDataIndexProvider : IndexProvider<AIChatSessionExtractedDataRecord>
{
    public AIChatSessionExtractedDataIndexProvider()
    {
        CollectionName = OrchardCoreAICollectionNames.AI;
    }

    public override void Describe(DescribeContext<AIChatSessionExtractedDataRecord> context)
    {
        context.For<AIChatSessionExtractedDataIndex>()
            .Map(record =>
            {
                var fieldNames = record.Values.Keys
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var valuesText = string.Join(
                    '\n',
                    record.Values
                        .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                        .SelectMany(pair => pair.Value.Select(value => $"{pair.Key}:{value}")));

                return new AIChatSessionExtractedDataIndex
                {
                    SessionId = record.SessionId,
                    ProfileId = record.ProfileId,
                    SessionStartedUtc = record.SessionStartedUtc,
                    SessionEndedUtc = record.SessionEndedUtc,
                    FieldCount = record.Values.Count,
                    FieldNames = string.Join('|', fieldNames),
                    ValuesText = valuesText,
                    UpdatedUtc = record.UpdatedUtc,
                };
            });
    }
}
