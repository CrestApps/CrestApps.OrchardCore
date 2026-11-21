using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores question banks.
/// </summary>
public sealed class QuestionBankStore : DocumentCatalog<QuestionBank, QuestionBankIndex>, INamedCatalog<QuestionBank>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionBankStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public QuestionBankStore(ISession session)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public async ValueTask<QuestionBank> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();

        return await Session.Query<QuestionBank, QuestionBankIndex>(index => index.Name == trimmed, collection: SurveysConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
