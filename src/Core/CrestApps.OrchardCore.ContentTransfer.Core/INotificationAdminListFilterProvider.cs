using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.ContentTransfer;

public interface IContentTransferEntryAdminListFilterProvider
{
    void Build(QueryEngineBuilder<ContentTransferEntry> builder);
}
