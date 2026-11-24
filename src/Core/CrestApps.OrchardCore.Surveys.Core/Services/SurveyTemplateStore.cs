using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores survey templates.
/// </summary>
public sealed class SurveyTemplateStore : DocumentCatalog<SurveyTemplate, SurveyTemplateIndex>, INamedCatalog<SurveyTemplate>
{
    private readonly SurveyContentSanitizer _sanitizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTemplateStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="sanitizer">Sanitizes the definition's HTML texts on every save; the module registers it.</param>
    public SurveyTemplateStore(ISession session, SurveyContentSanitizer sanitizer = null)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
        _sanitizer = sanitizer;
    }

    /// <inheritdoc />
    protected override ValueTask SavingAsync(SurveyTemplate record)
    {
        _sanitizer?.Sanitize(record?.Definition);

        return base.SavingAsync(record);
    }

    /// <inheritdoc />
    public async ValueTask<SurveyTemplate> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();

        return await Session.Query<SurveyTemplate, SurveyTemplateIndex>(index => index.Name == trimmed, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists every template by position, then title.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The templates.</returns>
    public async Task<IReadOnlyList<SurveyTemplate>> ListOrderedAsync(CancellationToken cancellationToken = default)
    {
        var items = await Session.Query<SurveyTemplate, SurveyTemplateIndex>(collection: SurveysConstants.CollectionName)
            .OrderBy(index => index.Position)
            .ThenBy(index => index.Title)
            .ListAsync(cancellationToken);

        return items.ToList();
    }
}
