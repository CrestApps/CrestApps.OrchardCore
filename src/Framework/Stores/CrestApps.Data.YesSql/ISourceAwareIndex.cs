using YesSql.Indexes;

namespace CrestApps.Data.YesSql;

public interface ISourceAwareIndex : IIndex
{
    string Source { get; set; }
}
