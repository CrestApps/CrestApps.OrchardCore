using System.Data;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.ContentTransfer;

public sealed class ValidatePartImportContext : ImportContentPartContext
{
    public ContentItem ContentItem { get; set; }

    public DataColumnCollection Columns { get; set; }

    public ContentValidateResult ContentValidateResult { get; } = new ContentValidateResult();
}
