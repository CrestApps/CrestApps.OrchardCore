using YesSql.Indexes;

namespace CrestApps.Data.YesSql;

public interface INameAwareIndex : IIndex
{
    string Name { get; set; }
}
