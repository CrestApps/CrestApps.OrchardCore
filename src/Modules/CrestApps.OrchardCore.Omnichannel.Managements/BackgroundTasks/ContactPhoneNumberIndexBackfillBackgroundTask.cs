using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Documents;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;

/// <summary>
/// Writes the phone number index rows of the contacts and leads saved before that index existed, so they can be found
/// by any of their numbers, up to 2,000 records a minute until every one has been read.
/// </summary>
/// <remarks>
/// A record is indexed again whenever it is saved, so only the records nothing has touched since need this. It runs
/// here rather than in the migration that creates the table, because re-saving every contact during activation would
/// hold up the tenant on a large list. Until it has finished, <see cref="Services.OmnichannelContactPhoneLookup"/>
/// also reads the cell and home columns of the contact index, so no record that used to be found goes missing. The
/// pages are read and saved by <see cref="ContactIndexBackfill"/>, which the email address step shares.
/// </remarks>
[BackgroundTask(
    Title = "Omnichannel Contact Phone Number Backfill",
    Schedule = "* * * * *",
    Description = "Indexes every phone number of the contacts and leads saved before each number was indexed.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class ContactPhoneNumberIndexBackfillBackgroundTask : IBackgroundTask
{
    internal const string BackfillName = "ContactPhoneNumbers";

    private const int PagesPerRun = 10;
    private const string ResultReindexed = "reindexed";

    private bool _completed;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(run => BackfillAsync(serviceProvider, run, cancellationToken));
    }

    private async Task BackfillAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        if (_completed)
        {
            run.Skip();

            return;
        }

        var stateManager = serviceProvider.GetRequiredService<IDocumentManager<OmnichannelBackfillState>>();
        var state = await stateManager.GetOrCreateMutableAsync();

        if (state.Completed.Contains(BackfillName))
        {
            _completed = true;
            run.Skip();

            return;
        }

        var step = await RunStepAsync(
            serviceProvider.GetRequiredService<IStore>(),
            serviceProvider.GetRequiredService<IContactMethodsAccessor>(),
            state.Cursors.GetValueOrDefault(BackfillName),
            PagesPerRun,
            serviceProvider.GetRequiredService<ILogger<ContactPhoneNumberIndexBackfillBackgroundTask>>(),
            cancellationToken);

        run.AddItems(step.Reindexed, ResultReindexed);

        if (step.Completed)
        {
            state.Completed.Add(BackfillName);
            state.Cursors.Remove(BackfillName);
            _completed = true;
        }
        else
        {
            state.Cursors[BackfillName] = step.Cursor;

            // The run read its pages and stopped; the records after its cursor wait for the next run.
            run.BudgetExceeded();
        }

        await stateManager.UpdateAsync(state);
    }

    /// <summary>
    /// Indexes up to <paramref name="pages"/> pages of records after <paramref name="cursor"/>.
    /// </summary>
    /// <param name="store">The store the records are read from and saved to.</param>
    /// <param name="contactMethods">Reads the phone numbers of a record.</param>
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
        var step = await ContactIndexBackfill.RunAsync(
            store,
            cursor,
            pages,
            item => contactMethods.Get(item).PhoneNumbers.Count > 0,
            ReadIndexedDocumentIdsAsync,
            cancellationToken);

        if (step.Reindexed > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Indexed every phone number of {RecordCount} contacts and leads saved before each number was indexed.", step.Reindexed);
        }

        return step;
    }

    private static async Task<IEnumerable<long>> ReadIndexedDocumentIdsAsync(ISession session, long[] documentIds, CancellationToken cancellationToken)
        => (await session
            .QueryIndex<OmnichannelContactPhoneNumberIndex>(index => index.DocumentId.IsIn(documentIds))
            .ListAsync(cancellationToken))
            .Select(index => index.DocumentId);
}
