using YesSql.Indexes;

namespace CrestApps.OrchardCore.YesSql.Core.Indexes;

public interface ISourceAwareIndex : IIndex
{
    string Source { get; set; }
}
