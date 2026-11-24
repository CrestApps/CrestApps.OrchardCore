using CrestApps.OrchardCore.Consent;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// The email suppression list as a consent source: an address that bounced for good, kept bouncing, or whose owner
/// reported the business's mail as spam is opted out of email, whoever holds it. The list is kept by the bounce and
/// complaint reports and by the people who manage it, so an opt-out or opt-in recorded through consent changes nothing
/// here.
/// </summary>
public sealed class EmailSuppressionConsentSource : IConsentSource
{
    /// <summary>
    /// The source's name.
    /// </summary>
    public const string SourceName = "email-suppression-list";

    private readonly IEmailSuppressionList _suppressions;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSuppressionConsentSource"/> class.
    /// </summary>
    /// <param name="suppressions">The suppression list.</param>
    public EmailSuppressionConsentSource(IEmailSuppressionList suppressions)
    {
        _suppressions = suppressions;
    }

    /// <inheritdoc />
    public string Name
        => SourceName;

    /// <inheritdoc />
    public async Task<bool> HasOptedOutAsync(ConsentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // An address on the list takes no email for any kind of record, so the record kind is not asked.
        if (!string.Equals(query.Channel, ConsentChannels.Email, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(query.Address))
        {
            return false;
        }

        return await _suppressions.FindAsync(query.Address, cancellationToken) is not null;
    }

    /// <inheritdoc />
    public Task RecordAsync(string channel, string address, ConsentChange change, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
