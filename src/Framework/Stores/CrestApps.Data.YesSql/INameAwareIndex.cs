using YesSql.Indexes;

namespace CrestApps.Data.YesSql;

public interface INameAwareIndex : IIndex
{
    string DisplayText { get; set; }
}
