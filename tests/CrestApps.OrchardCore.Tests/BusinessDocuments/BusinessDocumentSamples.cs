using CrestApps.OrchardCore.BusinessDocuments.Models;

namespace CrestApps.OrchardCore.Tests.BusinessDocuments;

/// <summary>
/// Business documents the renderer tests print.
/// </summary>
internal static class BusinessDocumentSamples
{
    /// <summary>
    /// An invoice with every block filled in, with item rows named <c>Item1</c>, <c>Item2</c> and so on.
    /// </summary>
    /// <param name="rows">How many item rows the invoice has.</param>
    public static BusinessDocument CreateInvoice(int rows = 3)
    {
        var document = new BusinessDocument
        {
            Kind = "Invoice",
            Title = "Invoice",
            Number = "INV-1001",
            Status = new BusinessDocumentStatus("Unpaid", BusinessDocumentTone.Warning),
            Issuer = new BusinessDocumentParty
            {
                Name = "Contoso Supplies",
                Lines = ["1 Main Street", "Springfield"],
                TaxId = "TAX-778",
                Email = "billing@contoso.example",
            },
            Recipient = new BusinessDocumentParty
            {
                Name = "Fabrikam Outfitters",
                Lines = ["9 Harbor Road"],
            },
            ShipTo = new BusinessDocumentParty
            {
                Name = "Fabrikam Warehouse",
                Lines = ["12 Dock Lane"],
            },
            Fields =
            [
                new BusinessDocumentField("Issue date", "May 1, 2026"),
                new BusinessDocumentField("Due date", "May 31, 2026"),
            ],
            Columns =
            [
                new BusinessDocumentColumn("Description", "Description", width: 4),
                new BusinessDocumentColumn("Quantity", "Qty", BusinessDocumentAlignment.End, 1),
                new BusinessDocumentColumn("Amount", "Amount", BusinessDocumentAlignment.End, 2),
            ],
            Totals =
            [
                new BusinessDocumentTotal("Subtotal", "$90.00"),
                new BusinessDocumentTotal("Sales tax", "$7.20", muted: true),
                new BusinessDocumentTotal("Amount due", "$97.20", emphasized: true),
            ],
            Sections =
            [
                new BusinessDocumentSection("Payment instructions", "Pay by bank transfer within 30 days."),
            ],
            Footer = "Thank you for your business.",
            AccentColor = "#2563EB",
        };

        for (var index = 1; index <= rows; index++)
        {
            document.Rows.Add(new BusinessDocumentRow
            {
                Cells =
                {
                    ["Description"] = $"Item{index}",
                    ["Quantity"] = "1",
                    ["Amount"] = "$30.00",
                },
            });
        }

        return document;
    }
}
