using YesSql.Indexes;

namespace CrestApps.OrchardCore.YesSql.Core;

public interface ISourceAwareIndex : IIndex
{
    string Source { get; set; }
}
