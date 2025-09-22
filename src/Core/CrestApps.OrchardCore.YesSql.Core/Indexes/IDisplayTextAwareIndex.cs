using YesSql.Indexes;

namespace CrestApps.OrchardCore.YesSql.Core.Indexes;

public interface IDisplayTextAwareIndex : IIndex
{
    string DisplayText { get; set; }
}
