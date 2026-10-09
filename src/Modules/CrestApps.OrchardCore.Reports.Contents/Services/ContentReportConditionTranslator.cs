using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.ContentManagement.Records;

namespace CrestApps.OrchardCore.Reports.Contents.Services;

/// <summary>
/// Translates the report conditions that map onto <see cref="ContentItemIndex"/> columns into index predicates, so the
/// database skips rows the report would drop anyway. A predicate never removes a row the report engine's own filter
/// keeps:
/// <list type="bullet">
/// <item><description>Range conditions on <c>CreatedUtc</c>, <c>ModifiedUtc</c>, and <c>PublishedUtc</c> become inclusive
/// bounds (a strict bound is widened to an inclusive one).</description></item>
/// <item><description><see cref="ReportFilterOperator.IsNotEmpty"/> on those columns and on <c>DisplayText</c>,
/// <c>Owner</c>, and <c>Author</c> becomes a not-null test.</description></item>
/// </list>
/// Text comparisons (<see cref="ReportFilterOperator.In"/>, <see cref="ReportFilterOperator.Contains"/>, and the other
/// text operators) are not translated: the report engine compares text ignoring case, while the database compares it
/// with the collation of the column, which is case-sensitive on some databases, so a translated condition could drop
/// rows the report keeps.
/// </summary>
public static class ContentReportConditionTranslator
{
    /// <summary>
    /// Translates the conditions that can be applied safely and skips the others.
    /// </summary>
    /// <param name="conditions">The conditions the report engine offers.</param>
    /// <returns>The index predicates to combine with a logical AND.</returns>
    public static IReadOnlyList<Expression<Func<ContentItemIndex, bool>>> Translate(IEnumerable<ReportDataCondition> conditions)
    {
        var predicates = new List<Expression<Func<ContentItemIndex, bool>>>();

        foreach (var condition in conditions ?? [])
        {
            if (condition is null || string.IsNullOrEmpty(condition.Field))
            {
                continue;
            }

            switch (condition.Field)
            {
                case ContentReportFieldNames.CreatedUtc:
                case ContentReportFieldNames.ModifiedUtc:
                case ContentReportFieldNames.PublishedUtc:
                    AddDateCondition(condition, predicates);
                    break;

                case ContentReportFieldNames.DisplayText:
                case ContentReportFieldNames.Owner:
                case ContentReportFieldNames.Author:
                    if (condition.Operator == ReportFilterOperator.IsNotEmpty)
                    {
                        predicates.Add(NotNull(condition.Field));
                    }

                    break;
            }
        }

        return predicates;
    }

    private static void AddDateCondition(ReportDataCondition condition, List<Expression<Func<ContentItemIndex, bool>>> predicates)
    {
        var values = condition.Values ?? [];
        var first = ToUtc(values.ElementAtOrDefault(0));

        switch (condition.Operator)
        {
            case ReportFilterOperator.GreaterThan:
            case ReportFilterOperator.GreaterThanOrEqual:
                if (first.HasValue)
                {
                    predicates.Add(AtLeast(condition.Field, first.Value));
                }

                break;

            case ReportFilterOperator.LessThan:
            case ReportFilterOperator.LessThanOrEqual:
                if (first.HasValue)
                {
                    predicates.Add(AtMost(condition.Field, first.Value));
                }

                break;

            case ReportFilterOperator.Between:
                var second = ToUtc(values.ElementAtOrDefault(1));

                if (first.HasValue)
                {
                    predicates.Add(AtLeast(condition.Field, first.Value));
                }

                if (second.HasValue)
                {
                    predicates.Add(AtMost(condition.Field, second.Value));
                }

                break;

            case ReportFilterOperator.IsNotEmpty:
                predicates.Add(NotNull(condition.Field));
                break;
        }
    }

    private static DateTime? ToUtc(object value)
    {
        return value switch
        {
            DateTime date when date.Kind == DateTimeKind.Local => date.ToUniversalTime(),
            DateTime date => date,
            DateTimeOffset offset => offset.UtcDateTime,
            _ => null,
        };
    }

    private static Expression<Func<ContentItemIndex, bool>> AtLeast(string field, DateTime value)
    {
        return field switch
        {
            ContentReportFieldNames.CreatedUtc => index => index.CreatedUtc >= value,
            ContentReportFieldNames.ModifiedUtc => index => index.ModifiedUtc >= value,
            _ => index => index.PublishedUtc >= value,
        };
    }

    private static Expression<Func<ContentItemIndex, bool>> AtMost(string field, DateTime value)
    {
        return field switch
        {
            ContentReportFieldNames.CreatedUtc => index => index.CreatedUtc <= value,
            ContentReportFieldNames.ModifiedUtc => index => index.ModifiedUtc <= value,
            _ => index => index.PublishedUtc <= value,
        };
    }

    private static Expression<Func<ContentItemIndex, bool>> NotNull(string field)
    {
        return field switch
        {
            ContentReportFieldNames.CreatedUtc => index => index.CreatedUtc != null,
            ContentReportFieldNames.ModifiedUtc => index => index.ModifiedUtc != null,
            ContentReportFieldNames.PublishedUtc => index => index.PublishedUtc != null,
            ContentReportFieldNames.DisplayText => index => index.DisplayText != null,
            ContentReportFieldNames.Owner => index => index.Owner != null,
            _ => index => index.Author != null,
        };
    }
}
