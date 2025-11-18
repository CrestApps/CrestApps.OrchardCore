using YesSql.Indexes;

namespace CrestApps.OrchardCore.YesSql.Core;

public interface INameAwareIndex : IIndex
{
    string DisplayText { get; set; }
}
