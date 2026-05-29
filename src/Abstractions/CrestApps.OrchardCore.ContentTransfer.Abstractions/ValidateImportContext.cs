using System.Data;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.ContentTransfer;

public sealed class ValidateImportContext : ImportContentContext
{
    public DataColumnCollection Columns { get; set; }

    public ContentValidateResult ContentValidateResult { get; } = new ContentValidateResult();
}
