using YesSql.Indexes;

namespace CrestApps.OrchardCore.YesSql.Core;

public interface IDisplayTextAwareIndex : IIndex
{
    string DisplayText { get; set; }
}
