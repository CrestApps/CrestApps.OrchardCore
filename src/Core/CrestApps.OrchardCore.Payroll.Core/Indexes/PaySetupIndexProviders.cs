using CrestApps.OrchardCore.Payroll.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Payroll.Core.Indexes;

/// <summary>
/// Maps pay schedules to <see cref="PayScheduleIndex"/> rows.
/// </summary>
public sealed class PayScheduleIndexProvider : IndexProvider<PaySchedule>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayScheduleIndexProvider"/> class.
    /// </summary>
    public PayScheduleIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PaySchedule> context)
    {
        context
            .For<PayScheduleIndex>()
            .Map(item => new PayScheduleIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Enabled = item.Enabled,
            });
    }
}

/// <summary>
/// Maps pay codes to <see cref="PayCodeIndex"/> rows.
/// </summary>
public sealed class PayCodeIndexProvider : IndexProvider<PayCode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayCodeIndexProvider"/> class.
    /// </summary>
    public PayCodeIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PayCode> context)
    {
        context
            .For<PayCodeIndex>()
            .Map(item => new PayCodeIndex
            {
                ItemId = item.ItemId,
                Code = item.Code,
                Name = item.Name,
                Class = item.Class.ToString(),
                Kind = item.Kind,
                Enabled = item.Enabled,
            });
    }
}

/// <summary>
/// The index providers of Pay Setup.
/// </summary>
public static class PaySetupIndexProviders
{
    /// <summary>
    /// Returns a new instance of each provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
        => [new PayScheduleIndexProvider(), new PayCodeIndexProvider()];
}
