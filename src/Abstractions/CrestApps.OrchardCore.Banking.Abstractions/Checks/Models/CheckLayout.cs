using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// How checks print: the paper, where the check sits on the sheet, where each field goes, the calibration, the
/// signatures and the check line. The signature images and the check line font are kept in the encrypted file store,
/// never in the media library.
/// </summary>
public sealed class CheckLayout : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the layout's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets notes about the layout, such as the stock it is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the paper.
    /// </summary>
    public CheckStock Stock { get; set; }

    /// <summary>
    /// Gets or sets where the check sits on the sheet.
    /// </summary>
    public CheckForm Form { get; set; }

    /// <summary>
    /// Gets or sets where each field prints, by name (<see cref="CheckFieldNames"/>). A field that is missing prints at
    /// its default place.
    /// </summary>
    public Dictionary<string, CheckFieldPosition> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets how far everything moves right, in points, to match the printer; negative moves left.
    /// </summary>
    public double OffsetX { get; set; }

    /// <summary>
    /// Gets or sets how far everything moves down, in points, to match the printer; negative moves up.
    /// </summary>
    public double OffsetY { get; set; }

    /// <summary>
    /// Gets or sets whether the check line (MICR) prints. It needs a check line font and, for the bank to read it, toner
    /// for magnetic printing.
    /// </summary>
    public bool PrintMicr { get; set; }

    /// <summary>
    /// Gets or sets the country whose check line format is used.
    /// </summary>
    public string MicrCountry { get; set; } = "US";

    /// <summary>
    /// Gets or sets whether the check number prints left of the routing number (the business check format). Off, it
    /// prints after the account number (the personal check format).
    /// </summary>
    public bool UseAuxiliaryOnUs { get; set; } = true;

    /// <summary>
    /// Gets or sets the size of the check line font, in points.
    /// </summary>
    public double MicrFontSize { get; set; } = 12;

    /// <summary>
    /// Gets or sets the characters of the check line font that draw the transit, amount, on-us and dash symbols, in
    /// that order. Most fonts draw them on A, B, C and D.
    /// </summary>
    public string MicrSymbolCharacters { get; set; } = "ABCD";

    /// <summary>
    /// Gets or sets the payer's name printed on blank stock.
    /// </summary>
    public string PayerName { get; set; }

    /// <summary>
    /// Gets or sets the payer's address printed on blank stock.
    /// </summary>
    public Address PayerAddress { get; set; }

    /// <summary>
    /// Gets or sets the amount above which a second signature line prints; <see langword="null"/> for never.
    /// </summary>
    public decimal? SecondSignatureAbove { get; set; }

    /// <summary>
    /// Gets or sets whether a signature image is stored.
    /// </summary>
    public bool HasSignature { get; set; }

    /// <summary>
    /// Gets or sets whether a second signature image is stored.
    /// </summary>
    public bool HasSecondSignature { get; set; }

    /// <summary>
    /// Gets or sets whether a check line font is stored.
    /// </summary>
    public bool HasMicrFont { get; set; }

    /// <summary>
    /// Gets or sets when the layout was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the layout was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
