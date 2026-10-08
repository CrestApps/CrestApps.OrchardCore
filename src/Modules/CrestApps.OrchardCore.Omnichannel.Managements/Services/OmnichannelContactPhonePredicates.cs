using System.Linq.Expressions;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Builds the one definition of "this contact has this phone number" that every phone search over the contact index
/// uses.
/// </summary>
/// <remarks>
/// The inventory load and the Manage Content <c>phone:</c> filters each carried their own copy of these predicates,
/// so a fix to one quietly left the other behind and the two screens could disagree about who holds a number. Both
/// now ask here. Every predicate is a plain expression over index columns, so YesSql turns it into SQL on every
/// database; nothing in it may call a method the SQL translator does not know.
/// </remarks>
internal static class OmnichannelContactPhonePredicates
{
    /// <summary>
    /// Builds the predicate that matches contacts whose primary cell or home number matches the search term.
    /// </summary>
    /// <param name="searchTerm">The parsed search term.</param>
    /// <param name="matchType">How the number is compared.</param>
    /// <returns>The predicate over the contact index.</returns>
    public static Expression<Func<OmnichannelContactIndex, bool>> Match(PhoneNumberSearchTerm searchTerm, PhoneNumberMatchType matchType)
    {
        var value = searchTerm.Value;

        if (matchType == PhoneNumberMatchType.Exact)
        {
            return Exact(searchTerm);
        }

        if (searchTerm.IsE164)
        {
            return matchType switch
            {
                PhoneNumberMatchType.BeginsWith => index =>
                    index.NormalizedPrimaryCellPhoneNumber.StartsWith(value) ||
                    index.NormalizedPrimaryHomePhoneNumber.StartsWith(value),
                PhoneNumberMatchType.EndsWith => index =>
                    index.NormalizedPrimaryCellPhoneNumber.EndsWith(value) ||
                    index.NormalizedPrimaryHomePhoneNumber.EndsWith(value),
                PhoneNumberMatchType.Contains => index =>
                    index.NormalizedPrimaryCellPhoneNumber.Contains(value) ||
                    index.NormalizedPrimaryHomePhoneNumber.Contains(value),
                _ => throw new ArgumentOutOfRangeException(nameof(matchType), matchType, "Unsupported phone number match type."),
            };
        }

        return matchType switch
        {
            PhoneNumberMatchType.BeginsWith => index =>
                index.PrimaryCellPhoneNumber.StartsWith(value) ||
                index.PrimaryHomePhoneNumber.StartsWith(value),
            PhoneNumberMatchType.EndsWith => index =>
                index.PrimaryCellPhoneNumber.EndsWith(value) ||
                index.PrimaryHomePhoneNumber.EndsWith(value),
            PhoneNumberMatchType.Contains => index =>
                index.PrimaryCellPhoneNumber.Contains(value) ||
                index.PrimaryHomePhoneNumber.Contains(value),
            _ => throw new ArgumentOutOfRangeException(nameof(matchType), matchType, "Unsupported phone number match type."),
        };
    }

    /// <summary>
    /// Builds the predicate that matches contacts whose primary cell and home numbers both do not contain the
    /// search term, the negation of a <see cref="PhoneNumberMatchType.Contains"/> match.
    /// </summary>
    /// <param name="searchTerm">The parsed search term.</param>
    /// <returns>The predicate over the contact index.</returns>
    public static Expression<Func<OmnichannelContactIndex, bool>> NotContains(PhoneNumberSearchTerm searchTerm)
    {
        var value = searchTerm.Value;

        if (searchTerm.IsE164)
        {
            return index =>
                index.NormalizedPrimaryCellPhoneNumber.NotContains(value) &&
                index.NormalizedPrimaryHomePhoneNumber.NotContains(value);
        }

        return index =>
            index.PrimaryCellPhoneNumber.NotContains(value) &&
            index.PrimaryHomePhoneNumber.NotContains(value);
    }

    /// <summary>
    /// An exact match finds the number in whichever shape it was stored in.
    /// </summary>
    /// <remarks>
    /// Comparing a national entry only with the national columns missed records whose number was written with its
    /// country code, and an E.164 entry only with the E.164 columns missed records that were never canonicalised. The
    /// term supplies each shape the number may take; each one is compared with the columns that hold that shape.
    /// </remarks>
    private static Expression<Func<OmnichannelContactIndex, bool>> Exact(PhoneNumberSearchTerm searchTerm)
    {
        Expression<Func<OmnichannelContactIndex, bool>> predicate = null;

        if (!searchTerm.IsE164)
        {
            var national = searchTerm.Value;

            predicate = index =>
                index.PrimaryCellPhoneNumber == national ||
                index.PrimaryHomePhoneNumber == national;
        }

        foreach (var e164 in searchTerm.GetExactE164Candidates())
        {
            predicate = OrElse(predicate, index =>
                index.NormalizedPrimaryCellPhoneNumber == e164 ||
                index.NormalizedPrimaryHomePhoneNumber == e164);
        }

        foreach (var national in searchTerm.GetExactUncanonicalNationalCandidates())
        {
            predicate = OrElse(predicate, index =>
                (index.PrimaryCellPhoneNumber == national && index.NormalizedPrimaryCellPhoneNumber == null) ||
                (index.PrimaryHomePhoneNumber == national && index.NormalizedPrimaryHomePhoneNumber == null));
        }

        // A parsed term always yields at least one shape, but a predicate that can never be built must still match
        // nobody rather than everybody.
        return predicate ?? (index => index.ContentItemId == string.Empty);
    }

    private static Expression<Func<OmnichannelContactIndex, bool>> OrElse(
        Expression<Func<OmnichannelContactIndex, bool>> left,
        Expression<Func<OmnichannelContactIndex, bool>> right)
    {
        if (left is null)
        {
            return right;
        }

        // Both halves have to speak about the same index row, so the right half is rewritten onto the left's
        // parameter before the two are joined.
        var body = new ParameterReplacer(right.Parameters[0], left.Parameters[0]).Visit(right.Body);

        return Expression.Lambda<Func<OmnichannelContactIndex, bool>>(Expression.OrElse(left.Body, body), left.Parameters[0]);
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ParameterReplacer(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _source ? _target : base.VisitParameter(node);
    }
}
