namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// The part one commerce module plays in exporting and anonymizing a person's data. A module registers its
/// contributor in the startup of the feature that owns the records, so the records of a disabled feature are never
/// read.
/// </summary>
/// <remarks>
/// A request runs in two phases. First every contributor is asked, in rounds, what else it knows identifies the
/// person: a checkout found by email reveals a guest owner, and a transaction reveals the checkout that paid it. The
/// rounds stop once a round adds nothing. Then every contributor exports, or anonymizes, the records that belong to
/// the identified person.
/// </remarks>
public interface ICommercePersonalDataContributor
{
    /// <summary>
    /// Adds what the module's records reveal about the person to the subject: other owners, checkout sessions, names,
    /// emails and payment-provider customers. It is called once per round, so it must be safe to call again.
    /// </summary>
    /// <param name="subject">The person, as identified so far.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the module's records about the person to the export.
    /// </summary>
    /// <param name="context">The identified person and the export being built.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the person's personal data from the module's records, keeping amounts, dates, statuses and document
    /// numbers, and counts what it changed on the result.
    /// </summary>
    /// <param name="context">The identified person, the replacement text and the result being built.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default);
}
