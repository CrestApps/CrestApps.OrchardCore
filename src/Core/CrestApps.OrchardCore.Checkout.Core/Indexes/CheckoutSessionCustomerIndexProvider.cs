using CrestApps.OrchardCore.Checkout;
using OrchardCore.Entities;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// Maps a <see cref="CheckoutSession"/> that has an owner or a contact email to a
/// <see cref="CheckoutSessionCustomerIndex"/> row. A session with neither identifies nobody, so it is not indexed.
/// </summary>
public sealed class CheckoutSessionCustomerIndexProvider : IndexProvider<CheckoutSession>
{
    /// <summary>
    /// The length of the <see cref="CheckoutSessionCustomerIndex.NormalizedEmail"/> column.
    /// </summary>
    public const int EmailLength = 255;

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CheckoutSession> context)
    {
        context.For<CheckoutSessionCustomerIndex>()
            .Map(session =>
            {
                session.TryGet<CheckoutContactInfo>(out var contact);

                var email = NormalizeEmail(contact?.Email);

                if (string.IsNullOrEmpty(session.OwnerId) && email is null)
                {
                    return null;
                }

                return new CheckoutSessionCustomerIndex
                {
                    SessionId = session.SessionId,
                    OwnerId = session.OwnerId,
                    NormalizedEmail = email,
                    CreatedUtc = session.CreatedUtc,
                };
            });
    }

    /// <summary>
    /// Returns an email as the index stores it: trimmed, upper-cased and cut to the column length, or
    /// <see langword="null"/> when it is blank. A query must normalize the email it looks for the same way.
    /// </summary>
    /// <param name="email">The email to normalize.</param>
    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToUpperInvariant();

        return normalized.Length <= EmailLength
            ? normalized
            : normalized.Substring(0, EmailLength);
    }
}
