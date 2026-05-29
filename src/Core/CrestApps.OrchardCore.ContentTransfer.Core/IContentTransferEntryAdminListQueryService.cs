using CrestApps.OrchardCore.ContentTransfer.Models;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.ContentTransfer;

public interface IContentTransferEntryAdminListQueryService
{
    Task<ContentTransferEntryQueryResult> QueryAsync(int page, int pageSize, ListContentTransferEntryOptions options, IUpdateModel updater);
}
