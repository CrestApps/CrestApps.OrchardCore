using CrestApps.OrchardCore.Wizard;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Wizard.Core.Indexes;

/// <summary>
/// Maps <see cref="WizardSession"/> documents to <see cref="WizardSessionIndex"/> rows.
/// </summary>
public sealed class WizardSessionIndexProvider : IndexProvider<WizardSession>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<WizardSession> context)
    {
        context.For<WizardSessionIndex>()
            .Map(session => new WizardSessionIndex
            {
                SessionId = session.SessionId,
                WizardType = session.WizardType,
                DefinitionId = session.DefinitionId,
                DefinitionVersionId = session.DefinitionVersionId,
                OwnerId = session.OwnerId,
                Status = session.Status,
                CreatedUtc = session.CreatedUtc,
                ModifiedUtc = session.ModifiedUtc,
                CompletedUtc = session.CompletedUtc,
            });
    }
}
