using YesSql.Indexes;

namespace CrestApps.OrchardCore.YesSql.Core.Indexes;

public interface INameAwareIndex : IIndex
{
    string Name { get; set; }
}
