using YesSql.Indexes;

namespace CrestApps.Data.YesSql;

public interface IDisplayTextAwareIndex : IIndex
{
    string DisplayText { get; set; }
}
