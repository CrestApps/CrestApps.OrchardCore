using CrestApps.OrchardCore.Workforce.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps sent shift reminders to <see cref="ShiftReminderLogIndex"/> rows.
/// </summary>
public sealed class ShiftReminderLogIndexProvider : IndexProvider<ShiftReminderLog>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftReminderLogIndexProvider"/> class.
    /// </summary>
    public ShiftReminderLogIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<ShiftReminderLog> context)
    {
        context.For<ShiftReminderLogIndex>()
            .Map(log => new ShiftReminderLogIndex
            {
                ItemId = log.ItemId,
                Key = log.Key,
                SentUtc = log.SentUtc,
            });
    }
}
