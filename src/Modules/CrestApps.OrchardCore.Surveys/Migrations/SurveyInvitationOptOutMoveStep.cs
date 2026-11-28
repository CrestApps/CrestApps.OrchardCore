using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Invitations;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Moves the opt-outs the old single opt-out list kept in one document into a record each
/// (<see cref="SurveyInvitationOptOut"/>), a page per run, and empties the list as it goes; the list keeps only its key.
/// Until a hash is moved, the opt-out store still honors it from the list. <see cref="SurveysMigrations"/> asks for it
/// when it adds the opt-out index.
/// </summary>
internal sealed class SurveyInvitationOptOutMoveStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Surveys.InvitationOptOutMove";

    /// <summary>
    /// The most hashes moved in one run.
    /// </summary>
    public const int PageSize = 500;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationOptOutMoveStep"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyInvitationOptOutMoveStep(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        var document = (await _session.Query<SurveyInvitationOptOutDocument>().ListAsync(cancellationToken))
            .OrderBy(item => item.Id)
            .FirstOrDefault(item => item.AddressHashes.Count > 0);

        if (document is null)
        {
            return true;
        }

        var page = document.AddressHashes.Take(PageSize).ToArray();
        var moved = (await _session.QueryIndex<SurveyInvitationOptOutIndex>(index => index.AddressHash.IsIn(page), collection: SurveysConstants.CollectionName)
                .ListAsync(cancellationToken))
            .Select(index => index.AddressHash)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var hash in page)
        {
            if (moved.Add(hash))
            {
                await _session.SaveAsync(new SurveyInvitationOptOut { AddressHash = hash }, collection: SurveysConstants.CollectionName, cancellationToken: cancellationToken);
            }

            document.AddressHashes.Remove(hash);
        }

        // The records and the emptier list commit together; an unsubscribe that changed the list meanwhile makes the save
        // fail, and the page is moved again on the next run.
        await _session.SaveAsync(document, checkConcurrency: true, cancellationToken: cancellationToken);

        return false;
    }
}
