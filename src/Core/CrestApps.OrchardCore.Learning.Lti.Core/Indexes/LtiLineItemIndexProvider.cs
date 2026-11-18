using CrestApps.OrchardCore.Learning.Lti.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Indexes;

/// <summary>
/// Maps line items.
/// </summary>
public sealed class LtiLineItemIndexProvider : IndexProvider<LtiLineItem>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LtiLineItemIndexProvider"/> class.
    /// </summary>
    public LtiLineItemIndexProvider()
    {
        CollectionName = LtiConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<LtiLineItem> context)
        => context.For<LtiLineItemIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(lineItem => new LtiLineItemIndex
            {
                ItemId = lineItem.ItemId,
                ToolId = lineItem.ToolId,
                CourseId = lineItem.CourseId,
                ResourceLinkId = lineItem.ResourceLinkId,
                ResourceId = Truncate(lineItem.ResourceId),
                Tag = Truncate(lineItem.Tag),
                CreatedUtc = lineItem.CreatedUtc,
            });

    private static string Truncate(string value)
        => value is { Length: > 255 } ? value.Substring(0, 255) : value;
}
