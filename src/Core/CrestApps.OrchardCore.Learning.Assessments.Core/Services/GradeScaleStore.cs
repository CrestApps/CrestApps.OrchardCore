using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Stores grade scales.
/// </summary>
public sealed class GradeScaleStore : DocumentCatalog<GradeScale, GradeScaleIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GradeScaleStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public GradeScaleStore(ISession session)
        : base(session)
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <summary>
    /// Finds a grade scale by name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scale, or <see langword="null"/>.</returns>
    public async Task<GradeScale> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(name)
            ? null
            : await Session.Query<GradeScale, GradeScaleIndex>(index => index.Name == name, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
}
