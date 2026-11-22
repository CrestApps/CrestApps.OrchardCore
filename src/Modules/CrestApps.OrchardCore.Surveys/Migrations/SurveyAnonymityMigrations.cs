using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Upgrades the anonymity data of sites made before it was hardened: protects every anonymity secret still stored in
/// plain text with ASP.NET Data Protection (keeping its value, so limits and invitation links keep working), gives a
/// secret to any survey without one, and clears the exact times kept on the participation records of anonymous surveys,
/// which keep the day only. It is registered after <see cref="SurveysMigrations"/>, whose tables it reads; on a new
/// site there is nothing to upgrade. Secrets missed here are still read, and new ones are always protected.
/// </summary>
internal sealed class SurveyAnonymityMigrations : DataMigration
{
    private readonly SurveyStore _surveyStore;
    private readonly SurveyAnonymitySecrets _secrets;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnonymityMigrations"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="secrets">The anonymity secrets.</param>
    /// <param name="session">The YesSql session.</param>
    public SurveyAnonymityMigrations(
        SurveyStore surveyStore,
        SurveyAnonymitySecrets secrets,
        ISession session)
    {
        _surveyStore = surveyStore;
        _secrets = secrets;
        _session = session;
    }

    /// <summary>
    /// Protects the secrets and clears the exact participation times of anonymous surveys.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        foreach (var survey in await _surveyStore.GetAllAsync())
        {
            if (_secrets.EnsureProtected(survey))
            {
                await _surveyStore.UpdateAsync(survey);
            }
        }

        await SurveyParticipationTimesUpgrade.ClearAnonymousCompletionTimesAsync(_session);

        return 1;
    }
}
