using System.Linq.Expressions;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Leaves the real responses of some groups (survey, collector, version, context kind) out of a query over the response
/// index, such as the groups of an anonymous survey too small to show (<see cref="Access.SurveyAnonymousAttributes"/>),
/// so a list's count and pages leave them out too. Test responses stay.
/// </summary>
public static class SurveyResponseGroupFilter
{
    /// <summary>
    /// The most groups one query leaves out. YesSql binds five parameters for each group (the test flag, survey, collector,
    /// version and context kind), so the groups take up to 1,000. A query may also name up to
    /// <see cref="Services.SurveyIdChunks.Size"/> (1,000) ids in one IN list, one parameter each, and about ten parameters
    /// of other filters (statuses, context kinds, grading, a user, dates): about 2,010 in the worst case, under SQL
    /// Server's limit of 2,100. A test runs that worst case on SQL Server. Raise neither this nor the chunk size without
    /// lowering the other.
    /// </summary>
    public const int MaxGroups = 200;

    /// <summary>
    /// Leaves the real responses of some groups out of a query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="groups">The groups, at most <see cref="MaxGroups"/>.</param>
    /// <returns>The narrowed query.</returns>
    public static IQuery<SurveyResponse, SurveyResponseIndex> Excluding(this IQuery<SurveyResponse, SurveyResponseIndex> query, IReadOnlyCollection<SurveyResponseGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (groups is null || groups.Count == 0)
        {
            return query;
        }

        if (groups.Count > MaxGroups)
        {
            throw new ArgumentException($"At most {MaxGroups} groups can be left out of one query.", nameof(groups));
        }

        foreach (var group in groups.Distinct())
        {
            query = query.Where(NotIn(group));
        }

        return query;
    }

    // "IsTest or not in the group", written with null-safe comparisons so a missing collector or context kind never
    // turns the condition unknown: IsTest || SurveyId <> s || collector differs || VersionNumber <> v || kind differs.
    private static Expression<Func<SurveyResponseIndex, bool>> NotIn(SurveyResponseGroup group)
    {
        var index = Expression.Parameter(typeof(SurveyResponseIndex), "index");
        var body = Expression.OrElse(
            Expression.Property(index, nameof(SurveyResponseIndex.IsTest)),
            Expression.NotEqual(Expression.Property(index, nameof(SurveyResponseIndex.SurveyId)), Expression.Constant(group.SurveyId ?? string.Empty, typeof(string))));

        body = Expression.OrElse(body, Differs(Expression.Property(index, nameof(SurveyResponseIndex.CollectorId)), group.CollectorId));
        body = Expression.OrElse(body, Expression.NotEqual(Expression.Property(index, nameof(SurveyResponseIndex.VersionNumber)), Expression.Constant(group.VersionNumber, typeof(int))));
        body = Expression.OrElse(body, Differs(Expression.Property(index, nameof(SurveyResponseIndex.ContextKind)), group.ContextKind));

        return Expression.Lambda<Func<SurveyResponseIndex, bool>>(body, index);
    }

    // A column differs from a value: for an empty value, the column holds something; otherwise it is empty or another value.
    private static BinaryExpression Differs(MemberExpression column, string value)
    {
        var none = Expression.Constant(null, typeof(string));
        var empty = Expression.Constant(string.Empty, typeof(string));

        return string.IsNullOrEmpty(value)
            ? Expression.AndAlso(Expression.NotEqual(column, none), Expression.NotEqual(column, empty))
            : Expression.OrElse(Expression.Equal(column, none), Expression.NotEqual(column, Expression.Constant(value, typeof(string))));
    }
}
