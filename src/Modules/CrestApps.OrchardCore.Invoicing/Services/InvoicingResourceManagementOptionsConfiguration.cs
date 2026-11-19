using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Registers the client-side resources of the invoicing editors.
/// </summary>
public sealed class InvoicingResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static InvoicingResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        // The line items editor of invoices, estimates, credit notes and recurring plans: lines added, removed and moved
        // in the browser, with amounts and totals worked out as you type (the server works them out again on save).
        _manifest
            .DefineScript("invoicing-document-lines")
            .SetUrl("~/CrestApps.OrchardCore.Invoicing/Scripts/document-lines-editor.min.js", "~/CrestApps.OrchardCore.Invoicing/Scripts/document-lines-editor.js")
            .SetVersion("1.0.0");

        // The payment schedule of a draft invoice: parts added and removed in the browser, given as amounts or percents.
        _manifest
            .DefineScript("invoicing-payment-schedule")
            .SetUrl("~/CrestApps.OrchardCore.Invoicing/Scripts/payment-schedule-editor.min.js", "~/CrestApps.OrchardCore.Invoicing/Scripts/payment-schedule-editor.js")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("invoicing-document-lines")
            .SetUrl("~/CrestApps.OrchardCore.Invoicing/Styles/document-lines-editor.min.css", "~/CrestApps.OrchardCore.Invoicing/Styles/document-lines-editor.css")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
