namespace CrestApps.OrchardCore.Disbursements.Core.Nacha;

/// <summary>
/// What a bank file (Nacha) holds: the origin, the business's account and the credits to send.
/// </summary>
public sealed class NachaFileRequest
{
    /// <summary>
    /// Gets or sets the origin settings of the business's account.
    /// </summary>
    public NachaOriginSettings Settings { get; set; }

    /// <summary>
    /// Gets or sets the routing number of the business's account; its first eight digits are the originating bank.
    /// </summary>
    public string OriginRoutingNumber { get; set; }

    /// <summary>
    /// Gets or sets the business's account number, for the offset entry.
    /// </summary>
    public string OriginAccountNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the business's account is a savings account, for the offset entry.
    /// </summary>
    public bool OriginIsSavings { get; set; }

    /// <summary>
    /// Gets or sets when the file is made, in the site's time zone.
    /// </summary>
    public DateTime CreatedLocal { get; set; }

    /// <summary>
    /// Gets or sets the file id modifier: A for the first file of the day, B for the second, and so on.
    /// </summary>
    public char FileIdModifier { get; set; } = 'A';

    /// <summary>
    /// Gets or sets the date the money should arrive.
    /// </summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the credits.
    /// </summary>
    public IList<NachaEntry> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets the sequence numbers the trace numbers use, in order, one per entry and offset entry, from a series
    /// kept per originating bank so no two files reuse one. When empty, the file numbers its entries from 1.
    /// </summary>
    public IList<long> TraceSequences { get; set; } = [];
}

/// <summary>
/// One credit in a bank file.
/// </summary>
public sealed class NachaEntry
{
    /// <summary>
    /// Gets or sets the key the caller knows the entry by, such as the disbursement id.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the payee is a business (a CCD entry) rather than a person (PPD).
    /// </summary>
    public bool IsBusiness { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry is a zero-amount test of the account (a prenote).
    /// </summary>
    public bool IsPrenote { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the payee's account is a savings account.
    /// </summary>
    public bool IsSavings { get; set; }

    /// <summary>
    /// Gets or sets the payee's routing number, nine digits.
    /// </summary>
    public string RoutingNumber { get; set; }

    /// <summary>
    /// Gets or sets the payee's account number.
    /// </summary>
    public string AccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the amount in cents.
    /// </summary>
    public long AmountCents { get; set; }

    /// <summary>
    /// Gets or sets the payee's id at the business, fifteen characters at most.
    /// </summary>
    public string IndividualId { get; set; }

    /// <summary>
    /// Gets or sets the payee's name, twenty-two characters at most.
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// A written bank file and the trace number each entry got.
/// </summary>
public sealed class NachaFileResult
{
    /// <summary>
    /// Gets or sets the file, 94-character records separated by line breaks, padded to a block of ten.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the trace number of each entry, by its key.
    /// </summary>
    public Dictionary<string, string> TraceNumbers { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the total of the credits, in cents.
    /// </summary>
    public long TotalCreditCents { get; set; }
}
