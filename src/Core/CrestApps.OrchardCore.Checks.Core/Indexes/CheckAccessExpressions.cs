using System.Linq.Expressions;
using CrestApps.OrchardCore.Checks.Models;
using YesSql.Services;

namespace CrestApps.OrchardCore.Checks.Core.Indexes;

/// <summary>
/// Turns a <see cref="CheckAccessFilter"/> into a condition on <see cref="CheckRecordIndex"/>, so the database reads
/// only the checks the reader may see.
/// </summary>
public static class CheckAccessExpressions
{
    /// <summary>
    /// Returns the condition, or <see langword="null"/> when the filter allows every check.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>The condition.</returns>
    public static Expression<Func<CheckRecordIndex, bool>> Restrict(CheckAccessFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.IsUnrestricted)
        {
            return null;
        }

        var restricted = filter.RestrictedKinds.ToArray();

        if (filter.AllowedKeys.Count == 0)
        {
            return index => index.SourceKind == null || index.SourceKind == string.Empty || index.SourceKind.IsNotIn(restricted);
        }

        var allowed = filter.AllowedKeys.ToArray();

        return index => index.SourceKind == null || index.SourceKind == string.Empty || index.SourceKind.IsNotIn(restricted) || index.AccessKey.IsIn(allowed);
    }
}
