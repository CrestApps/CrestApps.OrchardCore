using CrestApps.OrchardCore.Checks.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Checks.ViewModels;

/// <summary>
/// The check layout editor. Numbers are bound as invariant text.
/// </summary>
public class CheckLayoutViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the notes.
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
    /// Gets or sets the horizontal calibration, in points, as text.
    /// </summary>
    public string OffsetX { get; set; }

    /// <summary>
    /// Gets or sets the vertical calibration, in points, as text.
    /// </summary>
    public string OffsetY { get; set; }

    /// <summary>
    /// Gets or sets whether the check line prints.
    /// </summary>
    public bool PrintMicr { get; set; }

    /// <summary>
    /// Gets or sets whether the check number prints in the auxiliary on-us field.
    /// </summary>
    public bool UseAuxiliaryOnUs { get; set; }

    /// <summary>
    /// Gets or sets the check line font size, as text.
    /// </summary>
    public string MicrFontSize { get; set; }

    /// <summary>
    /// Gets or sets the characters the font draws the symbols on.
    /// </summary>
    public string MicrSymbolCharacters { get; set; }

    /// <summary>
    /// Gets or sets the payer's name printed on blank stock.
    /// </summary>
    public string PayerName { get; set; }

    /// <summary>
    /// Gets or sets the payer's street.
    /// </summary>
    public string PayerAddressLine1 { get; set; }

    /// <summary>
    /// Gets or sets the payer's second address line.
    /// </summary>
    public string PayerAddressLine2 { get; set; }

    /// <summary>
    /// Gets or sets the payer's city.
    /// </summary>
    public string PayerCity { get; set; }

    /// <summary>
    /// Gets or sets the payer's state or region.
    /// </summary>
    public string PayerRegion { get; set; }

    /// <summary>
    /// Gets or sets the payer's postal code.
    /// </summary>
    public string PayerPostalCode { get; set; }

    /// <summary>
    /// Gets or sets the amount above which a second signature prints, as text.
    /// </summary>
    public string SecondSignatureAbove { get; set; }

    /// <summary>
    /// Gets or sets the field places.
    /// </summary>
    public List<CheckFieldPositionViewModel> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets a new signature image.
    /// </summary>
    public IFormFile SignatureFile { get; set; }

    /// <summary>
    /// Gets or sets a new second signature image.
    /// </summary>
    public IFormFile SecondSignatureFile { get; set; }

    /// <summary>
    /// Gets or sets a new check line font.
    /// </summary>
    public IFormFile MicrFontFile { get; set; }

    /// <summary>
    /// Gets or sets whether to remove the signature image.
    /// </summary>
    public bool RemoveSignature { get; set; }

    /// <summary>
    /// Gets or sets whether to remove the second signature image.
    /// </summary>
    public bool RemoveSecondSignature { get; set; }

    /// <summary>
    /// Gets or sets whether to remove the check line font.
    /// </summary>
    public bool RemoveMicrFont { get; set; }

    /// <summary>
    /// Gets or sets whether a signature image is stored.
    /// </summary>
    [BindNever]
    public bool HasSignature { get; set; }

    /// <summary>
    /// Gets or sets whether a second signature image is stored.
    /// </summary>
    [BindNever]
    public bool HasSecondSignature { get; set; }

    /// <summary>
    /// Gets or sets whether a check line font is stored.
    /// </summary>
    [BindNever]
    public bool HasMicrFont { get; set; }

    /// <summary>
    /// Gets or sets the stock options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StockOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the form options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> FormOptions { get; set; } = [];
}
