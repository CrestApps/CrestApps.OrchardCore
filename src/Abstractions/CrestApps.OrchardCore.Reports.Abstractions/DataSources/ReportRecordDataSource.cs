using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// A data source made of <see cref="IReportRecordDataSet"/>s, each of which checks who may read it. Modules that
/// expose their records to the report builder derive from it and list their data sets. Data sets that implement
/// <see cref="IReportAggregateDataSet"/> also group and aggregate themselves.
/// </summary>
public abstract class ReportRecordDataSource : IReportDataSource, IReportAggregateDataSource
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract LocalizedString DisplayName { get; }

    /// <inheritdoc/>
    public abstract LocalizedString Description { get; }

    /// <summary>
    /// Gets the data sets of the source.
    /// </summary>
    protected abstract IEnumerable<IReportRecordDataSet> DataSets { get; }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        var dataSets = new List<ReportDataSetDescriptor>();

        if (context?.User is null)
        {
            return dataSets;
        }

        foreach (var dataSet in DataSets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await dataSet.CanReadAsync(context))
            {
                dataSet.Descriptor.DefaultDateField ??= dataSet.DefaultDateField;
                dataSets.Add(dataSet.Descriptor);
            }
        }

        return dataSets;
    }

    /// <inheritdoc/>
    public async Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        var found = await FindReadableAsync(dataSet, context);

        if (found is null)
        {
            return null;
        }

        found.Descriptor.DefaultDateField ??= found.DefaultDateField;

        return new ReportDataSetSchema
        {
            DataSet = found.Descriptor,
            Fields = found.Fields.ToList(),
        };
    }

    /// <inheritdoc/>
    public async Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var found = await FindReadableAsync(query.DataSet, query.Context);

        return found is null ? new ReportDataTable() : await found.QueryAsync(query, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ReportAggregateTable> AggregateAsync(ReportAggregateQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var found = await FindReadableAsync(query.DataSet, query.Context);

        return found is IReportAggregateDataSet aggregating
            ? await aggregating.AggregateAsync(query, cancellationToken)
            : null;
    }

    private async Task<IReportRecordDataSet> FindReadableAsync(string name, ReportDataSourceContext context)
    {
        if (string.IsNullOrEmpty(name) || context?.User is null)
        {
            return null;
        }

        var dataSet = DataSets.FirstOrDefault(candidate => string.Equals(candidate.Descriptor.Name, name, StringComparison.Ordinal));

        return dataSet is not null && await dataSet.CanReadAsync(context) ? dataSet : null;
    }
}

/// <summary>
/// Reads the date range that the conditions of a report put on one field, so a data source can ask its store for
/// that range only. The range is never narrower than the conditions: strict bounds become inclusive ones.
/// </summary>
public static class ReportDateRange
{
    /// <summary>
    /// Gets the range the conditions put on a date-time field.
    /// </summary>
    /// <param name="conditions">The conditions the report offers.</param>
    /// <param name="field">The field name.</param>
    /// <returns>The earliest and latest values in UTC; either is <see langword="null"/> when not bounded.</returns>
    public static (DateTime? From, DateTime? To) For(IEnumerable<ReportDataCondition> conditions, string field)
    {
        DateTime? from = null;
        DateTime? to = null;

        foreach (var condition in conditions ?? [])
        {
            if (condition is null || !string.Equals(condition.Field, field, StringComparison.Ordinal))
            {
                continue;
            }

            var first = ToUtc(condition.Values?.ElementAtOrDefault(0));

            switch (condition.Operator)
            {
                case ReportFilterOperator.GreaterThan:
                case ReportFilterOperator.GreaterThanOrEqual:
                    from = Later(from, first);
                    break;

                case ReportFilterOperator.LessThan:
                case ReportFilterOperator.LessThanOrEqual:
                    to = Earlier(to, first);
                    break;

                case ReportFilterOperator.Between:
                    from = Later(from, first);
                    to = Earlier(to, ToUtc(condition.Values?.ElementAtOrDefault(1)));
                    break;
            }
        }

        return (from, to);
    }

    private static DateTime? Later(DateTime? current, DateTime? candidate)
    {
        return candidate is null || (current.HasValue && current.Value >= candidate.Value) ? current : candidate;
    }

    private static DateTime? Earlier(DateTime? current, DateTime? candidate)
    {
        return candidate is null || (current.HasValue && current.Value <= candidate.Value) ? current : candidate;
    }

    private static DateTime? ToUtc(object value)
    {
        return value switch
        {
            DateTime { Kind: DateTimeKind.Local } date => date.ToUniversalTime(),
            DateTime date => DateTime.SpecifyKind(date, DateTimeKind.Utc),
            DateTimeOffset offset => offset.UtcDateTime,
            _ => null,
        };
    }
}
