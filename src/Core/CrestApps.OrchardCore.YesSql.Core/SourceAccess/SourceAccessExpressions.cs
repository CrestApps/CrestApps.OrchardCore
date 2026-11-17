using System.Linq.Expressions;
using System.Reflection;
using CrestApps.OrchardCore.SourceAccess;
using YesSql.Services;

namespace CrestApps.OrchardCore.YesSql.Core.SourceAccess;

/// <summary>
/// Turns a <see cref="SourceAccessFilter"/> into a condition on an index that keeps a <c>SourceKind</c> and an
/// <c>AccessKey</c> (<see cref="SourceAccessFilter.KeyOf"/>) column, so the database reads only the records the person
/// may see.
/// </summary>
public static class SourceAccessExpressions
{
    private static readonly MethodInfo _isIn = ((MethodCallExpression)((Expression<Func<string, bool>>)(value => value.IsIn(Array.Empty<string>()))).Body).Method;
    private static readonly MethodInfo _isNotIn = ((MethodCallExpression)((Expression<Func<string, bool>>)(value => value.IsNotIn(Array.Empty<string>()))).Body).Method;

    /// <summary>
    /// Returns the condition, or <see langword="null"/> when the filter allows every record.
    /// </summary>
    /// <typeparam name="TIndex">The index, with string <c>SourceKind</c> and <c>AccessKey</c> properties.</typeparam>
    /// <param name="filter">The filter.</param>
    /// <returns>The condition.</returns>
    public static Expression<Func<TIndex, bool>> Restrict<TIndex>(SourceAccessFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.IsUnrestricted)
        {
            return null;
        }

        var index = Expression.Parameter(typeof(TIndex), "index");
        var sourceKind = Expression.Property(index, Column<TIndex>("SourceKind"));
        var accessKey = Expression.Property(index, Column<TIndex>("AccessKey"));

        // No source kind, or one no source access restricts.
        Expression condition = Expression.OrElse(
            Expression.OrElse(
                Expression.Equal(sourceKind, Expression.Constant(null, typeof(string))),
                Expression.Equal(sourceKind, Expression.Constant(string.Empty))),
            Expression.Call(_isNotIn, sourceKind, Expression.Constant(filter.RestrictedKinds.ToArray(), _isNotIn.GetParameters()[1].ParameterType)));

        if (filter.AllowedKeys.Count > 0)
        {
            condition = Expression.OrElse(condition, Expression.Call(_isIn, accessKey, Expression.Constant(filter.AllowedKeys.ToArray(), _isIn.GetParameters()[1].ParameterType)));
        }

        return Expression.Lambda<Func<TIndex, bool>>(condition, index);
    }

    private static PropertyInfo Column<TIndex>(string name)
        => typeof(TIndex).GetProperty(name, typeof(string))
            ?? throw new InvalidOperationException($"The index '{typeof(TIndex).Name}' has no string column '{name}'.");
}
