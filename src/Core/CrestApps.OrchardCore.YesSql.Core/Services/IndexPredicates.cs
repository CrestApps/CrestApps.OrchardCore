using System.Linq.Expressions;

namespace CrestApps.OrchardCore.YesSql.Core.Services;

/// <summary>
/// Builds index query predicates YesSql can translate, such as an OR of conditions built in a loop.
/// </summary>
public static class IndexPredicates
{
    /// <summary>
    /// Returns a predicate that matches when either predicate matches, over one parameter, so YesSql translates it to SQL.
    /// </summary>
    /// <typeparam name="TIndex">The index type.</typeparam>
    /// <param name="left">The first predicate, or <see langword="null"/> to return <paramref name="right"/>.</param>
    /// <param name="right">The second predicate.</param>
    public static Expression<Func<TIndex, bool>> OrElse<TIndex>(Expression<Func<TIndex, bool>> left, Expression<Func<TIndex, bool>> right)
    {
        ArgumentNullException.ThrowIfNull(right);

        if (left is null)
        {
            return right;
        }

        var parameter = left.Parameters[0];
        var body = new ParameterRebinder(right.Parameters[0], parameter).Visit(right.Body);

        return Expression.Lambda<Func<TIndex, bool>>(Expression.OrElse(left.Body, body), parameter);
    }

    private sealed class ParameterRebinder : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ParameterRebinder(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _from ? _to : base.VisitParameter(node);
    }
}
