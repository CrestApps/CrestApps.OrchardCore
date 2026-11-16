using CrestApps.OrchardCore.Learning.Lti.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Indexes;

/// <summary>
/// Maps external tools.
/// </summary>
public sealed class ExternalToolIndexProvider : IndexProvider<ExternalTool>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalToolIndexProvider"/> class.
    /// </summary>
    public ExternalToolIndexProvider()
    {
        CollectionName = LtiConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<ExternalTool> context)
        => context.For<ExternalToolIndex>()
            .Map(tool => new ExternalToolIndex
            {
                ItemId = tool.ItemId,
                Name = tool.Name,
                ClientId = tool.ClientId,
                Enabled = tool.Enabled,
            });
}
