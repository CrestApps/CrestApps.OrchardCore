using System.Globalization;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Emails;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Exports the coupon codes issued to a person's e-mail address (such as a mailing list's welcome code) and, on erasure,
/// removes the address from them.
/// </summary>
/// <remarks>
/// An erased code is kept, not deleted: its idempotency key (a keyed hash, never the address) is what stops the same
/// address from getting a second code when it signs up again, and a used code stays for the checkouts and invoices that
/// redeemed it. Once its address is gone, a code that could still be used is turned off, because a code without an
/// address of its own falls back to its coupon's rules and would work for anyone who has it.
/// </remarks>
public sealed class IssuedCouponCodePersonalDataContributor : ICommercePersonalDataContributor
{
    /// <summary>
    /// The kind of the exported records and of the anonymized count.
    /// </summary>
    public const string RecordKind = "IssuedCouponCode";

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="IssuedCouponCodePersonalDataContributor"/> class.
    /// </summary>
    /// <param name="session">The tenant session the codes are read from and saved in.</param>
    public IssuedCouponCodePersonalDataContributor(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Gets a value indicating whether the contributor takes part when the guest retention window runs out. It does
    /// not: an issued code belongs to an address that signed up for it, not to a guest checkout, so only an explicit
    /// erasure request reaches it.
    /// </summary>
    public bool TakesPartInRetentionExpiry => false;

    /// <inheritdoc/>
    public Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var code in await FindAsync(context.Subject, cancellationToken))
        {
            context.Export.Records.Add(new CommercePersonalDataRecord
            {
                Kind = RecordKind,
                Id = code.ItemId,
                CreatedUtc = code.CreatedUtc,
                Values =
                {
                    [nameof(CouponCode.Code)] = code.Code,
                    [nameof(CouponCode.CustomerEmail)] = code.CustomerEmail,
                    ["IssuedUtc"] = Format(code.CreatedUtc),
                    [nameof(CouponCode.ExpiresUtc)] = code.ExpiresUtc is { } expires ? Format(expires) : null,
                    [nameof(CouponCode.Source)] = code.Source,
                    [nameof(CouponCode.RedemptionCount)] = code.RedemptionCount.ToString(CultureInfo.InvariantCulture),
                    [nameof(CouponCode.MaxRedemptions)] = code.MaxRedemptions?.ToString(CultureInfo.InvariantCulture),
                    [nameof(CouponCode.IsEnabled)] = code.IsEnabled ? "true" : "false",
                },
            });
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var code in await FindAsync(context.Subject, cancellationToken))
        {
            code.CustomerEmail = null;

            if (code.MaxRedemptions is null || code.RedemptionCount < code.MaxRedemptions)
            {
                code.IsEnabled = false;
            }

            await _session.SaveAsync(code, checkConcurrency: true, collection: CheckoutConstants.CouponCollectionName, cancellationToken: cancellationToken);
            context.Result.Count(RecordKind);
        }
    }

    private async Task<IReadOnlyList<CouponCode>> FindAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var emails = subject.Emails
            .Select(EmailAddresses.Normalize)
            .Where(email => email is { Length: <= EmailAddresses.MaxLength })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (emails.Length == 0)
        {
            return [];
        }

        var codes = await _session
            .Query<CouponCode, CouponCodeIndex>(index => index.NormalizedEmail.IsIn(emails), collection: CheckoutConstants.CouponCollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ThenBy(index => index.Id)
            .ListAsync(cancellationToken);

        return [.. codes];
    }

    private static string Format(DateTime utc)
        => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
}
