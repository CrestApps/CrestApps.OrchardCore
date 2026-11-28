using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Saves again, a batch at a time, every contact and lead with an email address that has no row in
/// <see cref="OmnichannelContactEmailAddressIndex"/> yet, so a record saved before that index existed is found by any of
/// its addresses. Saving the record also rewrites its <see cref="OmnichannelContactIndex"/> row, so its canonical
/// primary email is the one <see cref="OmnichannelEmailAddress.Normalize"/> gives.
/// </summary>
/// <remarks>
/// The contacts migration asks for it when it adds the index. It runs outside the tenant's activation, up to 2,000
/// records a minute, and it is safe to repeat: a record that already has its rows is left alone.
/// </remarks>
internal sealed class ContactEmailAddressIndexStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Omnichannel.ContactEmailAddresses";

    private const int PagesPerRun = 10;

    private readonly IStore _store;
    private readonly IContactMethodsAccessor _contactMethods;
    private readonly ILogger _logger;

    public ContactEmailAddressIndexStep(
        IStore store,
        IContactMethodsAccessor contactMethods,
        ILogger<ContactEmailAddressIndexStep> logger)
    {
        _store = store;
        _contactMethods = contactMethods;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cursor = long.TryParse(context.Cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

        var step = await RunStepAsync(_store, _contactMethods, cursor, PagesPerRun, _logger, cancellationToken);

        context.Cursor = step.Cursor.ToString(CultureInfo.InvariantCulture);

        return step.Completed;
    }

    /// <summary>
    /// Indexes up to <paramref name="pages"/> pages of records after <paramref name="cursor"/>.
    /// </summary>
    /// <param name="store">The store the records are read from and saved to.</param>
    /// <param name="contactMethods">Reads the email addresses of a record.</param>
    /// <param name="cursor">The document id of the last record already read.</param>
    /// <param name="pages">How many pages to read before returning.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The new cursor, whether every record has now been read, and how many records were indexed again.</returns>
    internal static async Task<(long Cursor, bool Completed, int Reindexed)> RunStepAsync(
        IStore store,
        IContactMethodsAccessor contactMethods,
        long cursor,
        int pages,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contactMethods);

        var step = await ContactIndexBackfill.RunAsync(
            store,
            cursor,
            pages,
            item => HasValidEmailAddress(contactMethods, item),
            ReadIndexedDocumentIdsAsync,
            cancellationToken);

        if (step.Reindexed > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Indexed every email address of {RecordCount} contacts and leads saved before each address was indexed.", step.Reindexed);
        }

        return step;
    }

    private static bool HasValidEmailAddress(IContactMethodsAccessor contactMethods, ContentItem item)
        => contactMethods.Get(item).EmailAddresses.Any(emailAddress => OmnichannelEmailAddress.IsValid(emailAddress?.Email));

    private static async Task<IEnumerable<long>> ReadIndexedDocumentIdsAsync(ISession session, long[] documentIds, CancellationToken cancellationToken)
        => (await session
            .QueryIndex<OmnichannelContactEmailAddressIndex>(index => index.DocumentId.IsIn(documentIds))
            .ListAsync(cancellationToken))
            .Select(index => index.DocumentId);
}
