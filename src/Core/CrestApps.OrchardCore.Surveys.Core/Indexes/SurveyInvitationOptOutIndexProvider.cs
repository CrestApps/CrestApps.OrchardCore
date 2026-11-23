using CrestApps.OrchardCore.Surveys.Core.Invitations;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Indexes the opt-out records of survey invitations by their address hash.
/// </summary>
public sealed class SurveyInvitationOptOutIndexProvider : IndexProvider<SurveyInvitationOptOut>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationOptOutIndexProvider"/> class.
    /// </summary>
    public SurveyInvitationOptOutIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SurveyInvitationOptOut> context)
        => context.For<SurveyInvitationOptOutIndex>()
            .Map(item => new SurveyInvitationOptOutIndex
            {
                AddressHash = item.AddressHash,
            });
}
