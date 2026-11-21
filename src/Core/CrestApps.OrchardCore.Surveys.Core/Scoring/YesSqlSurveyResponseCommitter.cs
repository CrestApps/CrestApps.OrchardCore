using CrestApps.OrchardCore.Surveys.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Scoring;

/// <summary>
/// Saves the pending changes of the YesSql session.
/// </summary>
public sealed class YesSqlSurveyResponseCommitter : ISurveyResponseCommitter
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="YesSqlSurveyResponseCommitter"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public YesSqlSurveyResponseCommitter(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public async Task<bool> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _session.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (ConcurrencyException)
        {
            return false;
        }
    }
}
