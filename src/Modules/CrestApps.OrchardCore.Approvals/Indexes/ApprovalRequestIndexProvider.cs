using CrestApps.OrchardCore.Approvals.Core.Indexes;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.SourceAccess;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Approvals.Indexes;

internal sealed class ApprovalRequestIndexProvider : IndexProvider<ApprovalRequest>
{
    public ApprovalRequestIndexProvider()
    {
        CollectionName = ApprovalsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<ApprovalRequest> context)
    {
        context
            .For<ApprovalRequestIndex>()
            .Map(request => new ApprovalRequestIndex
            {
                ItemId = request.ItemId,
                Status = request.Status.ToString(),
                SourceKind = request.SubjectKind,
                SubjectId = request.SubjectId,
                AccessKey = SourceAccessFilter.KeyOf(request.SubjectKind, request.AccessScope),
                PolicyId = request.PolicyId,
                RequesterUserId = request.RequesterUserId,
                CreatedUtc = request.CreatedUtc,
                DueUtc = request.DueUtc,
                ModifiedUtc = request.ModifiedUtc ?? request.CreatedUtc,
            });
    }
}
