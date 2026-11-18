using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The private file store that keeps the PDF of each issued invoice and credit note, never served as a public file. The
/// Invoicing feature registers it with <c>AddTenantFileStore</c>: under the tenant's <c>App_Data</c> folder, or in Azure
/// Blob Storage when the shell configuration names a container.
/// </summary>
public sealed class InvoiceArchiveFileStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceArchiveFileStore"/> class.
    /// </summary>
    /// <param name="fileStore">The tenant file store it keeps the PDFs in.</param>
    public InvoiceArchiveFileStore(IFileStore fileStore)
    {
        FileStore = fileStore;
    }

    /// <summary>
    /// Gets the tenant file store.
    /// </summary>
    public IFileStore FileStore { get; }
}
